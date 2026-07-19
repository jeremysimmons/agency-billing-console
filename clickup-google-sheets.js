/**
 * ClickUp → Google Sheets
 *
 * Main functions:
 *
 *   fetchClickUpTasksUpsert()
 *     Retrieves tasks matching the configured ClickUp query.
 *     Upserts rows by task URL.
 *     Preserves manual columns B–F.
 *
 *   backfillClickUpTimeData()
 *     Independently retrieves time_estimate and time_spent for every
 *     existing task row in the sheet, including tasks outside the
 *     date filter used by fetchClickUpTasksUpsert().
 *
 * Columns:
 *
 *   A: url
 *   B–F: manual columns
 *   G–T: ClickUp API columns
 */

// ======== CONFIG ========
const CLICKUP_API_TOKEN = '...';
const CLICKUP_TEAM_ID = '9014205398';
const CLICKUP_ASSIGNEE_ID = '82383480';

const DATE_GT_JAN_2025 = '1735689600000';
// Jan 1, 2025 UTC in milliseconds

const SHEET_NAME = 'tasks';
const PAGE_LIMIT = 100;

// Pause between individual Get Task requests during backfill.
const BACKFILL_REQUEST_DELAY_MS = 100;
// ========================

const TASK_HEADERS = [
  'url',                 // A
  'Bill',                // B: manual
  'Billable Hours',      // C: manual
  'NonBillable Hours',   // D: manual
  'invoice',             // E: manual
  'Note',                // F: manual

  'project_name',        // G
  'list_name',           // H
  'name',                // I
  'description',         // J
  'parent',              // K
  'status',              // L
  'tags',                // M
  'date_created',        // N
  'due_date',            // O
  'date_done',           // P
  'date_closed',         // Q
  'order_index',         // R
  'estimated_hours',     // S
  'actual_hours',        // T
];

/**
 * Import and upsert ClickUp tasks matching the configured query.
 *
 * Existing rows:
 *   Updates only columns G–T.
 *
 * New rows:
 *   Adds the task URL in A, leaves B–F blank, and writes API data to G–T.
 *
 * This function updates time data for tasks returned by the query, but it
 * does not independently retrieve older tasks outside DATE_GT_JAN_2025.
 * Run backfillClickUpTimeData() separately to update every existing row.
 */
function fetchClickUpTasksUpsert() {
  const sheet = getTaskSheet_();
  ensureHeaders_(sheet, TASK_HEADERS);

  const clickupApiToken = getClickUpToken_();

  const baseUrl =
    `https://api.clickup.com/api/v2/team/${CLICKUP_TEAM_ID}/task` +
    `?reverse=true` +
    `&include_closed=true` +
    `&assignees[]=${encodeURIComponent(CLICKUP_ASSIGNEE_ID)}` +
    `&date_created_gt=${encodeURIComponent(DATE_GT_JAN_2025)}` +
    `&subtasks=true`;

  const existingMap = readExistingUrlMap_(sheet);

  const rowsToAppend = [];
  const updates = [];

  let page = 0;
  let retrievedTaskCount = 0;

  while (true) {
    const requestUrl =
      `${baseUrl}` +
      `&page=${page}` +
      `&limit=${PAGE_LIMIT}`;

    Logger.log(`Retrieving ClickUp page ${page}: ${requestUrl}`);

    const response = fetchClickUpJson_(
      requestUrl,
      clickupApiToken
    );

    const tasks = Array.isArray(response.tasks)
      ? response.tasks
      : [];

    retrievedTaskCount += tasks.length;

    for (const task of tasks) {
      const urlKey = safeStr_(task.url);
      const valuesGtoT = buildTaskApiValues_(task);

      if (urlKey && existingMap.has(urlKey)) {
        updates.push({
          row: existingMap.get(urlKey),
          values: valuesGtoT,
        });
      } else {
        rowsToAppend.push([
          urlKey,             // A
          '', '', '', '', '', // B–F: manual columns
          ...valuesGtoT,      // G–T
        ]);
      }
    }

    if (response.last_page || tasks.length === 0) {
      break;
    }

    page++;
  }

  Logger.log(
    `Retrieved ${retrievedTaskCount} tasks. ` +
    `${updates.length} updates and ${rowsToAppend.length} new rows.`
  );

  updateExistingTaskRows_(sheet, updates);
  appendTaskRows_(sheet, rowsToAppend);

  formatTaskSheet_(sheet);

  Logger.log('ClickUp task import completed.');
}

/**
 * Backfill time data for every existing row in the task sheet.
 *
 * This is a separate function and is not automatically run by
 * fetchClickUpTasksUpsert().
 *
 * For each existing task URL in column A, this function:
 *
 *   1. Extracts the ClickUp task ID.
 *   2. Calls the Get Task endpoint.
 *   3. Reads time_estimate and time_spent.
 *   4. Writes:
 *        S: estimated_hours
 *        T: actual_hours
 *
 * Existing S–T values are preserved when:
 *
 *   - the URL does not contain a recognizable task ID;
 *   - the task was deleted;
 *   - the API token cannot access the task;
 *   - the API request fails.
 */
function backfillClickUpTimeData() {
  const sheet = getTaskSheet_();
  ensureHeaders_(sheet, TASK_HEADERS);

  const lastRow = sheet.getLastRow();

  if (lastRow < 2) {
    Logger.log('No task rows were found to backfill.');
    return;
  }

  const clickupApiToken = getClickUpToken_();
  const rowCount = lastRow - 1;

  const taskUrls = sheet
    .getRange(2, 1, rowCount, 1)
    .getValues();

  const existingTimeValues = sheet
    .getRange(2, 19, rowCount, 2)
    .getValues();

  const updatedTimeValues = [];

  let updatedCount = 0;
  let skippedCount = 0;
  let totalEstimatedMilliseconds = 0;
  let totalActualMilliseconds = 0;

  for (let index = 0; index < taskUrls.length; index++) {
    const sheetRow = index + 2;
    const taskUrl = safeStr_(taskUrls[index][0]);
    const taskId = extractClickUpTaskId_(taskUrl);

    if (!taskId) {
      Logger.log(
        `Row ${sheetRow}: unable to extract task ID from "${taskUrl}".`
      );

      updatedTimeValues.push(existingTimeValues[index]);
      skippedCount++;
      continue;
    }

    try {
      const task = fetchClickUpTaskById_(
        taskId,
        clickupApiToken
      );

      const estimatedMilliseconds = normalizeDurationMilliseconds_(
        task.time_estimate
      );

      const actualMilliseconds = normalizeDurationMilliseconds_(
        task.time_spent
      );

      const estimatedHours = millisecondsToHours_(
        estimatedMilliseconds
      );

      const actualHours = millisecondsToHours_(
        actualMilliseconds
      );

      updatedTimeValues.push([
        estimatedHours,
        actualHours,
      ]);

      totalEstimatedMilliseconds += estimatedMilliseconds;
      totalActualMilliseconds += actualMilliseconds;
      updatedCount++;

      Logger.log(
        `Row ${sheetRow}: task ${taskId}; ` +
        `estimated=${estimatedHours}; actual=${actualHours}`
      );
    } catch (error) {
      Logger.log(
        `Row ${sheetRow}: unable to retrieve task ${taskId}. ` +
        `${error.message}`
      );

      // Preserve current values instead of replacing them with zero.
      updatedTimeValues.push(existingTimeValues[index]);
      skippedCount++;
    }

    if (BACKFILL_REQUEST_DELAY_MS > 0) {
      Utilities.sleep(BACKFILL_REQUEST_DELAY_MS);
    }
  }

  // Write all backfilled values in one Sheets operation.
  sheet
    .getRange(2, 19, updatedTimeValues.length, 2)
    .setValues(updatedTimeValues)
    .setNumberFormat('0.00');

  Logger.log(
    `Time backfill completed. ` +
    `Updated: ${updatedCount}; skipped: ${skippedCount}.`
  );

  Logger.log(
    `Total estimated hours retrieved: ` +
    `${millisecondsToHours_(totalEstimatedMilliseconds)}`
  );

  Logger.log(
    `Total actual hours retrieved: ` +
    `${millisecondsToHours_(totalActualMilliseconds)}`
  );
}

/**
 * Build the API-driven values for columns G–T.
 */
function buildTaskApiValues_(task) {
  let projectName = safeStr_(
    task.project && task.project.name
  );

  const listName = safeStr_(
    task.list && task.list.name
  );

  if (projectName === 'hidden') {
    projectName = listName;
  }

  const estimatedMilliseconds =
    normalizeDurationMilliseconds_(task.time_estimate);

  const actualMilliseconds =
    normalizeDurationMilliseconds_(task.time_spent);

  return [
    projectName,                              // G
    listName,                                 // H
    safeStr_(task.name),                      // I
    safeStr_(task.description),               // J
    safeStr_(task.parent),                    // K
    safeStr_(task.status && task.status.status), // L
    formatTags_(task.tags),                   // M
    toYMDHM_(task.date_created),              // N
    toYMDHM_(task.due_date),                  // O
    toYMDHM_(task.date_done),                 // P
    toYMDHM_(task.date_closed),               // Q
    safeStr_(task.orderindex),                // R
    millisecondsToHours_(estimatedMilliseconds), // S
    millisecondsToHours_(actualMilliseconds),    // T
  ];
}

/**
 * Update existing task rows.
 *
 * Each update writes API-driven columns G–T only, preserving A–F.
 */
function updateExistingTaskRows_(sheet, updates) {
  Logger.log(`Updating ${updates.length} existing rows.`);

  for (const update of updates) {
    sheet
      .getRange(
        update.row,
        7,
        1,
        14
      )
      .setValues([update.values]);
  }
}

/**
 * Append new task rows.
 */
function appendTaskRows_(sheet, rowsToAppend) {
  if (rowsToAppend.length === 0) {
    return;
  }

  const startRow = sheet.getLastRow() + 1;

  sheet
    .getRange(
      startRow,
      1,
      rowsToAppend.length,
      TASK_HEADERS.length
    )
    .setValues(rowsToAppend);
}

/**
 * Retrieve one ClickUp task by its task ID.
 */
function fetchClickUpTaskById_(taskId, clickupApiToken) {
  const requestUrl =
    `https://api.clickup.com/api/v2/task/` +
    `${encodeURIComponent(taskId)}` +
    `?include_subtasks=true`;

  return fetchClickUpJson_(
    requestUrl,
    clickupApiToken
  );
}

/**
 * Perform a ClickUp GET request and decode the JSON response.
 *
 * Retries rate-limit responses and temporary server errors.
 */
function fetchClickUpJson_(requestUrl, clickupApiToken) {
  const maxAttempts = 5;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    const response = UrlFetchApp.fetch(requestUrl, {
      method: 'get',
      headers: {
        Authorization: clickupApiToken,
        Accept: 'application/json',
      },
      muteHttpExceptions: true,
    });

    const responseCode = response.getResponseCode();
    const responseBody = response.getContentText();

    if (responseCode >= 200 && responseCode < 300) {
      try {
        return JSON.parse(responseBody);
      } catch (error) {
        throw new Error(
          `ClickUp returned invalid JSON: ${error.message}`
        );
      }
    }

    const retryable =
      responseCode === 429 ||
      responseCode === 500 ||
      responseCode === 502 ||
      responseCode === 503 ||
      responseCode === 504;

    if (retryable && attempt < maxAttempts) {
      const delayMilliseconds = 1500 * attempt;

      Logger.log(
        `ClickUp returned HTTP ${responseCode}. ` +
        `Retrying attempt ${attempt + 1} of ${maxAttempts} ` +
        `after ${delayMilliseconds} ms.`
      );

      Utilities.sleep(delayMilliseconds);
      continue;
    }

    throw new Error(
      `ClickUp API error HTTP ${responseCode}: ${responseBody}`
    );
  }

  throw new Error(
    `ClickUp request failed after ${maxAttempts} attempts.`
  );
}

/**
 * Extract a task ID from a ClickUp task URL.
 *
 * Supported examples:
 *
 *   https://app.clickup.com/t/86abc123
 *   https://app.clickup.com/t/9014205398/86abc123
 *   https://app.clickup.com/t/86abc123?comment=123
 */
function extractClickUpTaskId_(taskUrl) {
  if (!taskUrl) {
    return '';
  }

  const cleanUrl = String(taskUrl)
    .trim()
    .split('?')[0]
    .split('#')[0]
    .replace(/\/+$/, '');

  const match = cleanUrl.match(
    /\/t\/(?:[^/]+\/)?([^/]+)$/i
  );

  return match
    ? decodeURIComponent(match[1])
    : '';
}

/**
 * Build a map of task URL → sheet row.
 */
function readExistingUrlMap_(sheet) {
  const map = new Map();
  const lastRow = sheet.getLastRow();

  if (lastRow < 2) {
    return map;
  }

  const values = sheet
    .getRange(2, 1, lastRow - 1, 1)
    .getValues();

  for (let index = 0; index < values.length; index++) {
    const taskUrl = values[index][0];

    if (taskUrl) {
      map.set(
        String(taskUrl),
        index + 2
      );
    }
  }

  return map;
}

/**
 * Get or create the configured task sheet.
 */
function getTaskSheet_() {
  const spreadsheet = SpreadsheetApp.getActive();

  return (
    spreadsheet.getSheetByName(SHEET_NAME) ||
    spreadsheet.insertSheet(SHEET_NAME)
  );
}

/**
 * Ensure row 1 contains the expected headers.
 */
function ensureHeaders_(sheet, headers) {
  const currentHeaders =
    sheet.getLastRow() >= 1
      ? sheet
          .getRange(1, 1, 1, headers.length)
          .getValues()[0]
      : [];

  const headersMatch =
    currentHeaders.length === headers.length &&
    currentHeaders.every(
      (value, index) =>
        String(value) === headers[index]
    );

  if (!headersMatch) {
    sheet
      .getRange(1, 1, 1, headers.length)
      .setValues([headers]);
  }
}

/**
 * Apply number formatting and resize columns.
 */
function formatTaskSheet_(sheet) {
  const lastRow = sheet.getLastRow();

  if (lastRow >= 2) {
    sheet
      .getRange(
        2,
        19,
        lastRow - 1,
        2
      )
      .setNumberFormat('0.00');
  }

  sheet.autoResizeColumns(
    1,
    TASK_HEADERS.length
  );
}

/**
 * Format ClickUp tags as a semicolon-separated string.
 */
function formatTags_(tags) {
  if (!Array.isArray(tags)) {
    return '';
  }

  return tags
    .map(tag =>
      tag && tag.name
        ? String(tag.name)
        : ''
    )
    .filter(Boolean)
    .join(';');
}

/**
 * Convert a ClickUp epoch timestamp to yyyy-MM-dd HH:mm.
 *
 * Accepts timestamps expressed in seconds or milliseconds.
 */
function toYMDHM_(epochLike) {
  if (!epochLike) {
    return '';
  }

  let timestamp = Number(epochLike);

  if (
    !Number.isFinite(timestamp) ||
    timestamp <= 0
  ) {
    return '';
  }

  // A timestamp this small is most likely expressed in seconds.
  if (timestamp < 1e11) {
    timestamp *= 1000;
  }

  const timezone =
    Session.getScriptTimeZone() ||
    'UTC';

  return Utilities.formatDate(
    new Date(timestamp),
    timezone,
    'yyyy-MM-dd HH:mm'
  );
}

/**
 * Normalize a ClickUp duration value to milliseconds.
 */
function normalizeDurationMilliseconds_(value) {
  const milliseconds = Number(value);

  if (
    !Number.isFinite(milliseconds) ||
    milliseconds <= 0
  ) {
    return 0;
  }

  return milliseconds;
}

/**
 * Convert milliseconds to decimal hours.
 *
 * Examples:
 *
 *   3,600,000 ms → 1
 *   5,400,000 ms → 1.5
 */
function millisecondsToHours_(milliseconds) {
  const value = Number(milliseconds);

  if (
    !Number.isFinite(value) ||
    value <= 0
  ) {
    return 0;
  }

  return Math.round(
    (value / 3600000) * 100
  ) / 100;
}

/**
 * Safely convert nullable values to strings.
 */
function safeStr_(value) {
  return value == null
    ? ''
    : String(value);
}

/**
 * Retrieve the ClickUp API token from Apps Script Properties.
 *
 * Configure it under:
 *
 *   Project Settings
 *   → Script Properties
 *   → CLICKUP_API_TOKEN
 */
function getClickUpToken_() {
  const token = PropertiesService
    .getScriptProperties()
    .getProperty('CLICKUP_API_TOKEN');

  if (!token) {
    throw new Error(
      'ClickUp API token is not set in Script Properties.'
    );
  }

  return token;
}