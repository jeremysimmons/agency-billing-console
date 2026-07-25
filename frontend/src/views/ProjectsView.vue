<script setup lang="ts">
import { computed, ref } from 'vue'
import { useClients } from '../queries/clients'
import { useAllProjects, useCreateProject, useUpdateProject } from '../queries/projects'
import type { Project } from '../api/types'

const SHARED_CLIENT_NAME = 'Shared'

const { data: clients } = useClients()
const { data: projects, isLoading, error } = useAllProjects()
const createProject = useCreateProject()
const updateProject = useUpdateProject()

const sharedClient = computed(() =>
  (clients.value ?? []).find((c) => c.name.trim().toLowerCase() === SHARED_CLIENT_NAME.toLowerCase()),
)

const name = ref('')
const formError = ref('')
const editingId = ref<string | null>(null)
const editName = ref('')
const editClientId = ref('')
const editError = ref('')

const sortedProjects = computed(() =>
  (projects.value ?? []).slice().sort((a, b) => {
    const byClient = a.clientName.localeCompare(b.clientName, undefined, { sensitivity: 'base' })
    if (byClient !== 0) return byClient
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
  }),
)

async function add() {
  formError.value = ''
  if (!sharedClient.value) {
    formError.value = 'Shared client is missing. Restart the API to seed it.'
    return
  }
  try {
    await createProject.mutateAsync({
      clientId: sharedClient.value.id,
      name: name.value.trim(),
    })
    name.value = ''
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not create project.'
  }
}

function startEdit(p: Project) {
  editingId.value = p.id
  editName.value = p.name
  editClientId.value = p.clientId
  editError.value = ''
}

function cancelEdit() {
  editingId.value = null
  editName.value = ''
  editClientId.value = ''
  editError.value = ''
}

async function saveEdit() {
  if (!editingId.value) return
  const trimmed = editName.value.trim()
  if (!trimmed) {
    editError.value = 'Project name is required.'
    return
  }
  if (!editClientId.value) {
    editError.value = 'Client is required.'
    return
  }
  editError.value = ''
  try {
    await updateProject.mutateAsync({
      id: editingId.value,
      name: trimmed,
      clientId: editClientId.value,
    })
    cancelEdit()
  } catch (e: any) {
    editError.value = e?.response?.data?.error ?? 'Could not save project.'
  }
}
</script>

<template>
  <section data-testid="projects-view">
    <h1>Projects</h1>
    <p class="hint">
      New projects are added under the <strong>Shared</strong> client. Edit a project to move it to a specific client (or back to Shared).
    </p>

    <form class="row" data-testid="project-create-form" @submit.prevent="add">
      <input v-model="name" placeholder="Project name" required data-testid="project-create-name" />
      <button
        :disabled="createProject.isLoading.value || !name.trim() || !sharedClient"
        data-testid="project-create-submit"
      >Add to Shared</button>
    </form>
    <p v-if="formError" class="error" data-testid="project-create-error">{{ formError }}</p>
    <p v-if="editError" class="error" data-testid="project-edit-error">{{ editError }}</p>

    <p v-if="isLoading" data-testid="projects-loading">Loading…</p>
    <p v-else-if="error" class="error" data-testid="projects-error">Failed to load projects.</p>
    <table v-else class="grid" data-testid="projects-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Client</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in sortedProjects" :key="p.id" :data-testid="`project-row-${p.id}`">
          <template v-if="editingId !== p.id">
            <td :data-testid="`project-name-${p.id}`">{{ p.name }}</td>
            <td :data-testid="`project-client-${p.id}`">
              <RouterLink :to="`/clients/${p.clientId}`">{{ p.clientName }}</RouterLink>
            </td>
            <td>
              <button
                type="button"
                class="link"
                :data-testid="`project-edit-${p.id}`"
                @click="startEdit(p)"
              >Edit</button>
            </td>
          </template>
          <td v-else colspan="3">
            <form
              class="edit-row"
              :data-testid="`project-edit-form-${p.id}`"
              @submit.prevent="saveEdit"
            >
              <input
                v-model="editName"
                required
                :data-testid="`project-edit-name-${p.id}`"
              />
              <select v-model="editClientId" required :data-testid="`project-edit-client-${p.id}`">
                <option v-for="c in clients" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
              <button
                type="submit"
                :disabled="updateProject.isLoading.value"
                :data-testid="`project-edit-save-${p.id}`"
              >Save</button>
              <button
                type="button"
                class="link"
                :data-testid="`project-edit-cancel-${p.id}`"
                @click="cancelEdit"
              >Cancel</button>
            </form>
          </td>
        </tr>
        <tr v-if="sortedProjects.length === 0">
          <td colspan="3" data-testid="projects-empty">No projects yet.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.hint { color: #6b7280; font-size: 0.9rem; margin: -0.35rem 0 1rem; }
.row, .edit-row { display: flex; gap: 0.5rem; margin-bottom: 1rem; flex-wrap: wrap; align-items: center; }
.edit-row { margin: 0; }
input, select {
  padding: 0.5rem 0.7rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font: inherit;
}
button:not(.link) {
  padding: 0.5rem 0.9rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
button:disabled { opacity: 0.6; cursor: default; }
.link { background: none; border: none; color: #10b981; cursor: pointer; padding: 0; font: inherit; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #eee; vertical-align: middle; }
.error { color: #dc2626; }
</style>
