<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useClients, useUpdateClient, useDeleteClient } from '../queries/clients'
import { useProjects, useCreateProject } from '../queries/projects'

const props = defineProps<{ id: string }>()
const router = useRouter()
const clientId = computed(() => props.id)

const { data: clients } = useClients()
const client = computed(() => clients.value?.find((c) => c.id === clientId.value))

const { data: projects } = useProjects(clientId)
const createProject = useCreateProject()
const updateClient = useUpdateClient()
const deleteClient = useDeleteClient()

const editing = ref(false)
const editName = ref('')
const renameError = ref('')
const confirmDelete = ref(false)
const deleteError = ref('')

watch(client, (c) => {
  if (c && !editing.value) editName.value = c.name
}, { immediate: true })

function startRename() {
  editName.value = client.value?.name ?? ''
  renameError.value = ''
  editing.value = true
}

function cancelRename() {
  editing.value = false
  editName.value = client.value?.name ?? ''
  renameError.value = ''
}

async function saveRename() {
  const c = client.value
  if (!c) return
  renameError.value = ''
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
    renameError.value = e?.response?.data?.error ?? 'Could not rename client.'
  }
}

const pName = ref('')
const projError = ref('')

async function addProject() {
  projError.value = ''
  try {
    await createProject.mutateAsync({ clientId: clientId.value, name: pName.value })
    pName.value = ''
  } catch (e: any) {
    projError.value = e?.response?.data?.error ?? 'Could not create project.'
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
  <section>
    <p><RouterLink to="/clients">← Clients</RouterLink></p>

    <template v-if="client">
      <div v-if="!editing" class="title-row">
        <h1>{{ client.name }}</h1>
        <button class="link" @click="startRename">Rename</button>
      </div>
      <form v-else class="row" @submit.prevent="saveRename">
        <input v-model="editName" required />
        <button type="submit" :disabled="updateClient.isLoading.value">Save</button>
        <button type="button" class="link" @click="cancelRename">Cancel</button>
      </form>
      <p v-if="renameError" class="error">{{ renameError }}</p>
      <p class="meta">
        Code: {{ client.code ?? '—' }}
        · Folder id: {{ client.clickUpFolderId ?? '—' }}
        · <RouterLink :to="{ path: '/tasks', query: { clientId: client.id } }">View tasks</RouterLink>
      </p>

      <h2>Projects</h2>
      <p class="hint">Your projects (not ClickUp lists). Assign tasks to these on the Tasks page.</p>
      <form class="row" @submit.prevent="addProject">
        <input v-model="pName" placeholder="Project name" required />
        <button :disabled="createProject.isLoading.value">Add project</button>
      </form>
      <p v-if="projError" class="error">{{ projError }}</p>
      <ul>
        <li v-for="p in projects" :key="p.id">{{ p.name }}</li>
        <li v-if="projects && projects.length === 0" class="muted">No projects yet.</li>
      </ul>

      <div class="danger-zone">
        <template v-if="!confirmDelete">
          <button class="link danger" @click="confirmDelete = true">Delete client</button>
        </template>
        <template v-else>
          <span>Delete {{ client.name }} and its projects/tasks?</span>
          <button class="link danger" :disabled="deleteClient.isLoading.value" @click="remove">Confirm</button>
          <button class="link" @click="confirmDelete = false">Cancel</button>
        </template>
        <p v-if="deleteError" class="error">{{ deleteError }}</p>
      </div>
    </template>
    <p v-else>Client not found.</p>
  </section>
</template>

<style scoped>
.title-row { display: flex; align-items: baseline; gap: 0.75rem; }
.row { display: flex; gap: 0.5rem; margin-bottom: 0.75rem; flex-wrap: wrap; }
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
</style>
