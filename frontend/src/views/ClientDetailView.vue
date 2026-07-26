<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useClients, useUpdateClient, useDeleteClient } from '../queries/clients'
import { useProjects, useCreateProject, useUpdateProject } from '../queries/projects'

const props = defineProps<{ id: string }>()
const router = useRouter()
const clientId = computed(() => props.id)

const { data: clients } = useClients()
const client = computed(() => clients.value?.find((c) => c.id === clientId.value))

const { data: projects } = useProjects(clientId)
const createProject = useCreateProject()
const updateProject = useUpdateProject()
const updateClient = useUpdateClient()
const deleteClient = useDeleteClient()

const editing = ref(false)
const editName = ref('')
const editError = ref('')
const confirmDelete = ref(false)
const deleteError = ref('')

watch(client, (c) => {
  if (c && !editing.value) {
    editName.value = c.name
  }
}, { immediate: true })

function startEdit() {
  editName.value = client.value?.name ?? ''
  editError.value = ''
  editing.value = true
}

function cancelEdit() {
  editing.value = false
  editName.value = client.value?.name ?? ''
  editError.value = ''
}

async function saveEdit() {
  const c = client.value
  if (!c) return
  editError.value = ''
  try {
    await updateClient.mutateAsync({
      id: clientId.value,
      input: {
        name: editName.value.trim(),
        code: c.code,
        originalName: c.originalName,
        description: c.description,
        status: c.status,
        active: c.active,
      },
    })
    editing.value = false
  } catch (e: any) {
    editError.value = e?.response?.data?.error ?? 'Could not save client.'
  }
}

const pName = ref('')
const projError = ref('')
const editingProjectId = ref<string | null>(null)
const projectEditName = ref('')
const projectEditError = ref('')

async function addProject() {
  projError.value = ''
  try {
    await createProject.mutateAsync({ clientId: clientId.value, name: pName.value })
    pName.value = ''
  } catch (e: any) {
    projError.value = e?.response?.data?.error ?? 'Could not create project.'
  }
}

function startProjectEdit(id: string, name: string) {
  editingProjectId.value = id
  projectEditName.value = name
  projectEditError.value = ''
}

function cancelProjectEdit() {
  editingProjectId.value = null
  projectEditName.value = ''
  projectEditError.value = ''
}

async function saveProjectEdit() {
  if (!editingProjectId.value) return
  const name = projectEditName.value.trim()
  if (!name) {
    projectEditError.value = 'Project name is required.'
    return
  }
  projectEditError.value = ''
  try {
    await updateProject.mutateAsync({
      id: editingProjectId.value,
      clientId: clientId.value,
      name,
    })
    cancelProjectEdit()
  } catch (e: any) {
    projectEditError.value = e?.response?.data?.error ?? 'Could not rename project.'
  }
}

async function remove() {
  deleteError.value = ''
  try {
    await deleteClient.mutateAsync(clientId.value)
    router.push('/clients')
  } catch (e: any) {
    deleteError.value = e?.response?.data?.error ?? 'Could not delete client.'
    confirmDelete.value = false
  }
}
</script>

<template>
  <section data-testid="client-detail-view">
    <p>
      <RouterLink to="/clients" data-testid="client-detail-back">← Clients</RouterLink> &nbsp; 
      <RouterLink v-if="client"Assign tasks to these on the Tasks page :to="{ path: '/tasks', query: { clientId: client.id } }" data-testid="client-detail-tasks-link">View tasks</RouterLink>
    
    </p>

    <template v-if="client">
      <div v-if="!editing" class="title-row">
        <h1 data-testid="client-detail-name">{{ client.name }}</h1>
        <button class="link" data-testid="client-detail-edit" @click="startEdit">Edit</button>
      </div>
      <form v-else class="edit-form" data-testid="client-edit-form" @submit.prevent="saveEdit">
        <label>
          Name
          <input v-model="editName" required data-testid="client-edit-name" />
        </label>
        <div class="row">
          <button type="submit" :disabled="updateClient.isLoading.value" data-testid="client-edit-save">Save</button>
          <button type="button" class="link" data-testid="client-edit-cancel" @click="cancelEdit">Cancel</button>
        </div>
      </form>
      <p v-if="editError" class="error" data-testid="client-edit-error">{{ editError }}</p>
      <p v-if="!editing" class="meta" data-testid="client-detail-meta">
        Original name: {{ client.originalName ?? '—' }}<br/>
        Folder id: {{ client.clickUpFolderId ?? '—' }}<br/>
        List id: {{ client.clickUpListId ?? '—' }}<br/>
      </p>

      <h2>Projects</h2>
      <p class="hint">not ClickUp lists</p>
      <form class="row" data-testid="project-create-form" @submit.prevent="addProject">
        <input v-model="pName" placeholder="Project name" required data-testid="project-create-name" />
        <button :disabled="createProject.isLoading.value" data-testid="project-create-submit">Add project</button>
      </form>
      <p v-if="projError" class="error" data-testid="project-create-error">{{ projError }}</p>
      <ul data-testid="projects-list">
        <li v-for="p in projects" :key="p.id" class="project-item" :data-testid="`project-item-${p.id}`">
          <template v-if="editingProjectId !== p.id">
            <span>{{ p.name }}</span>
            <button
              type="button"
              class="link"
              :data-testid="`project-rename-${p.id}`"
              @click="startProjectEdit(p.id, p.name)"
            >Rename</button>
          </template>
          <form
            v-else
            class="project-edit-form"
            :data-testid="`project-edit-form-${p.id}`"
            @submit.prevent="saveProjectEdit"
          >
            <input
              v-model="projectEditName"
              required
              :data-testid="`project-edit-name-${p.id}`"
            />
            <button
              type="submit"
              :disabled="updateProject.isLoading.value"
              :data-testid="`project-edit-save-${p.id}`"
            >Save</button>
            <button
              type="button"
              class="link"
              :data-testid="`project-edit-cancel-${p.id}`"
              @click="cancelProjectEdit"
            >Cancel</button>
          </form>
        </li>
        <li v-if="projects && projects.length === 0" class="muted" data-testid="projects-empty">No projects yet.</li>
      </ul>
      <p v-if="projectEditError" class="error" data-testid="project-edit-error">{{ projectEditError }}</p>

      <div class="danger-zone">
        <template v-if="!confirmDelete">
          <button class="link danger" data-testid="client-delete" @click="confirmDelete = true">Delete client</button>
        </template>
        <template v-else>
          <span data-testid="client-delete-prompt">Delete {{ client.name }} and its projects/tasks?</span>
          <button class="link danger" :disabled="deleteClient.isLoading.value" data-testid="client-delete-confirm" @click="remove">Confirm</button>
          <button class="link" data-testid="client-delete-cancel" @click="confirmDelete = false">Cancel</button>
        </template>
        <p v-if="deleteError" class="error" data-testid="client-delete-error">{{ deleteError }}</p>
      </div>
    </template>
    <p v-else data-testid="client-not-found">Client not found.</p>
  </section>
</template>

<style scoped>
.title-row { display: flex; align-items: baseline; gap: 0.75rem; }
.edit-form { display: flex; flex-direction: column; gap: 0.6rem; margin-bottom: 0.75rem; max-width: 24rem; }
.edit-form label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #6b7280; }
.row { display: flex; gap: 0.5rem; margin-bottom: 0.75rem; flex-wrap: wrap; align-items: center; }
input { padding: 0.5rem 0.7rem; border: 1px solid #d1d5db; border-radius: 8px; }
button:not(.link) {
  padding: 0.5rem 0.9rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
}
.link { background: none; border: none; color: #10b981; cursor: pointer; padding: 0; font: inherit; }
.danger { color: #b91c1c; }
.meta, .hint, .muted { color: #6b7280; font-size: 0.9rem; }
.error { color: #b91c1c; }
.danger-zone { margin-top: 2rem; display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; }
ul { padding-left: 1.2rem; }
.project-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.35rem;
}
.project-edit-form {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}
</style>
