<script setup lang="ts">
import { ref } from 'vue'
import {
  useUnmappedContainers,
  useUnmappedWorkItems,
  useStatusMappings,
  useSuggestMappings,
  useConfirmContainer,
  useIgnoreContainer,
  useConfirmTask,
  useIgnoreTask,
  useApplyStatuses,
} from '../queries/mappings'

const { data: containers, isLoading: loadingContainers } = useUnmappedContainers()
const { data: workItems, isLoading: loadingTasks } = useUnmappedWorkItems()
const { data: statuses } = useStatusMappings()

const suggest = useSuggestMappings()
const confirmContainer = useConfirmContainer()
const ignoreContainer = useIgnoreContainer()
const confirmTask = useConfirmTask()
const ignoreTask = useIgnoreTask()
const applyStatuses = useApplyStatuses()

const message = ref('')
const error = ref('')

async function runSuggest() {
  error.value = ''; message.value = ''
  try {
    const r = await suggest.mutateAsync()
    message.value = `Suggested ${r.containerSuggestions} containers, ${r.taskSuggestions} tasks; seeded ${r.statusSeeded} statuses.`
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Suggest failed.'
  }
}

async function createClientFrom(containerId: string) {
  error.value = ''
  try {
    await confirmContainer.mutateAsync({ containerId, createClient: true })
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Confirm failed.'
  }
}

async function createProjectFrom(containerId: string, clientId: string | null) {
  error.value = ''
  try {
    await confirmContainer.mutateAsync({
      containerId,
      createProject: true,
      clientId,
      createClient: !clientId,
    })
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Confirm failed.'
  }
}

async function acceptSuggestedContainer(containerId: string, clientId: string | null, projectId: string | null) {
  error.value = ''
  try {
    await confirmContainer.mutateAsync({ containerId, clientId, projectId })
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Confirm failed.'
  }
}

async function createInternalTask(workItemId: string) {
  error.value = ''
  try {
    await confirmTask.mutateAsync({ workItemId, createTask: true })
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Create task failed.'
  }
}

async function acceptSuggestedTask(workItemId: string, taskId: string) {
  error.value = ''
  try {
    await confirmTask.mutateAsync({ workItemId, taskId })
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Confirm failed.'
  }
}

async function runApplyStatuses() {
  error.value = ''; message.value = ''
  try {
    const r = await applyStatuses.mutateAsync()
    message.value = `Updated status on ${r.updated} mapped tasks.`
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Apply statuses failed.'
  }
}
</script>

<template>
  <section>
    <h1>ClickUp mappings</h1>
    <p class="lead">Map folders/lists to clients/projects and work items to tasks. Confirmed mappings are never overwritten silently.</p>

    <div class="actions">
      <button @click="runSuggest" :disabled="suggest.isLoading.value">Suggest matches</button>
      <button class="secondary" @click="runApplyStatuses" :disabled="applyStatuses.isLoading.value">Apply statuses to mapped tasks</button>
    </div>
    <p v-if="message" class="ok">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <h2>Unmapped containers</h2>
    <p v-if="loadingContainers">Loading…</p>
    <table v-else class="grid">
      <thead>
        <tr><th>Type</th><th>Name</th><th>Parent</th><th>Status</th><th>Suggestion</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="c in containers" :key="c.containerId">
          <td>{{ c.containerType }}</td>
          <td>
            <a v-if="c.url" :href="c.url" target="_blank" rel="noopener">{{ c.name }}</a>
            <span v-else>{{ c.name }}</span>
          </td>
          <td>
            <span v-if="c.parentName">{{ c.parentName }}</span>
            <span v-else class="muted">—</span>
          </td>
          <td>{{ c.mappingStatus ?? '—' }}</td>
          <td>
            <span v-if="c.suggestedClientName">client: {{ c.suggestedClientName }}</span>
            <span v-if="c.suggestedProjectName"> · project: {{ c.suggestedProjectName }}</span>
            <span v-if="!c.suggestedClientName && !c.suggestedProjectName">—</span>
          </td>
          <td class="btns">
            <button
              v-if="c.suggestedClientId || c.suggestedProjectId"
              class="link"
              @click="acceptSuggestedContainer(c.containerId, c.suggestedClientId, c.suggestedProjectId)"
            >Accept</button>
            <button v-if="c.containerType === 'Folder' || c.containerType === 'Space'" class="link" @click="createClientFrom(c.containerId)">Create client</button>
            <button v-if="c.containerType === 'List'" class="link" @click="createProjectFrom(c.containerId, c.suggestedClientId)">Create project</button>
            <button class="link danger" @click="ignoreContainer.mutateAsync(c.containerId)">Ignore</button>
          </td>
        </tr>
        <tr v-if="containers && containers.length === 0"><td colspan="6">Nothing to review.</td></tr>
      </tbody>
    </table>

    <h2>Unmapped work items</h2>
    <p v-if="loadingTasks">Loading…</p>
    <table v-else class="grid">
      <thead>
        <tr><th>Name</th><th>List</th><th>ClickUp status</th><th>Suggestion</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="w in workItems" :key="w.workItemId">
          <td>
            <a v-if="w.url" :href="w.url" target="_blank" rel="noopener">{{ w.name }}</a>
            <span v-else>{{ w.name }}</span>
          </td>
          <td>{{ w.containerName ?? '—' }}</td>
          <td>{{ w.statusName ?? '—' }}</td>
          <td>{{ w.suggestedTaskTitle ?? w.mappingStatus ?? '—' }}</td>
          <td class="btns">
            <button v-if="w.suggestedTaskId" class="link" @click="acceptSuggestedTask(w.workItemId, w.suggestedTaskId)">Accept</button>
            <button class="link" @click="createInternalTask(w.workItemId)">Create task</button>
            <button class="link danger" @click="ignoreTask.mutateAsync(w.workItemId)">Ignore</button>
          </td>
        </tr>
        <tr v-if="workItems && workItems.length === 0"><td colspan="5">Nothing to review.</td></tr>
      </tbody>
    </table>

    <h2>Status mappings</h2>
    <table class="grid">
      <thead>
        <tr><th>ClickUp</th><th>Internal</th><th>Completed</th><th>Billable</th></tr>
      </thead>
      <tbody>
        <tr v-for="s in statuses" :key="s.id">
          <td>{{ s.externalStatusName }}</td>
          <td>{{ s.internalStatus }}</td>
          <td>{{ s.treatedAsCompleted ? 'yes' : 'no' }}</td>
          <td>{{ s.treatedAsBillable ? 'yes' : 'no' }}</td>
        </tr>
        <tr v-if="statuses && statuses.length === 0"><td colspan="4">Run Suggest to seed defaults.</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.lead { opacity: 0.8; margin-bottom: 1rem; }
.actions { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
button { padding: 0.5rem 0.9rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
button.secondary { background: #374151; }
.grid { width: 100%; border-collapse: collapse; margin-bottom: 2rem; font-size: 0.95rem; }
.grid th, .grid td { text-align: left; padding: 0.45rem 0.5rem; border-bottom: 1px solid #eee; vertical-align: top; }
.btns { display: flex; flex-wrap: wrap; gap: 0.4rem; }
.link { background: none; color: #10b981; padding: 0; }
.link.danger { color: #dc2626; }
a { color: #059669; text-decoration: underline; text-underline-offset: 2px; }
.ok { color: #047857; }
.error { color: #dc2626; }
.muted { opacity: 0.55; }
h2 { margin-top: 1.5rem; }
</style>
