<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useClients, useUpdateClient, useDeleteClient } from '../queries/clients'
import { useProjects, useCreateProject } from '../queries/projects'
import { useTasks, useCreateTask } from '../queries/tasks'

const props = defineProps<{ id: string }>()
const router = useRouter()
const clientId = computed(() => props.id)

const { data: clients } = useClients()
const client = computed(() => clients.value?.find((c) => c.id === clientId.value))

const { data: projects } = useProjects(clientId)
const { data: tasks } = useTasks(clientId)
const createProject = useCreateProject()
const createTask = useCreateTask()
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
const pRate = ref<number | null>(null)
const projError = ref('')

async function addProject() {
  projError.value = ''
  try {
    await createProject.mutateAsync({
      clientId: clientId.value, name: pName.value,
      billingType: 'Hourly', hourlyRate: pRate.value,
    })
    pName.value = ''; pRate.value = null
  } catch (e: any) {
    projError.value = e?.response?.data?.error ?? 'Could not create project.'
  }
}

const tTitle = ref('')
const tProjectId = ref<string>('')
const tParentId = ref<string>('')
const tEstimate = ref<number | null>(null)
const taskError = ref('')

async function addTask() {
  taskError.value = ''
  try {
    await createTask.mutateAsync({
      clientId: clientId.value,
      title: tTitle.value,
      projectId: tProjectId.value || null,
      parentTaskId: tParentId.value || null,
      estimatedMinutes: tEstimate.value,
    })
    tTitle.value = ''; tProjectId.value = ''; tParentId.value = ''; tEstimate.value = null
  } catch (e: any) {
    taskError.value = e?.response?.data?.error ?? 'Could not create task.'
  }
}

async function removeClient() {
  deleteError.value = ''
  try {
    await deleteClient.mutateAsync(clientId.value)
    await router.push('/clients')
  } catch (e: any) {
    deleteError.value = e?.response?.data?.error ?? 'Could not delete client.'
    confirmDelete.value = false
  }
}
</script>

<template>
  <section>
    <RouterLink to="/clients">← Clients</RouterLink>
    <div class="head">
      <div class="title">
        <template v-if="editing">
          <form class="rename" @submit.prevent="saveRename">
            <input v-model="editName" required autofocus />
            <button type="submit" :disabled="updateClient.isLoading.value">Save</button>
            <button type="button" class="secondary" :disabled="updateClient.isLoading.value" @click="cancelRename">Cancel</button>
          </form>
          <p v-if="renameError" class="error">{{ renameError }}</p>
        </template>
        <template v-else>
          <h1>{{ client?.name ?? 'Client' }}</h1>
          <button class="link" @click="startRename">Rename</button>
        </template>
      </div>
      <div class="danger-zone">
        <template v-if="confirmDelete">
          <p class="confirm-text">Delete <strong>{{ client?.name }}</strong>? This cannot be undone.</p>
          <button class="danger" :disabled="deleteClient.isLoading.value" @click="removeClient">Yes, delete</button>
          <button class="secondary" :disabled="deleteClient.isLoading.value" @click="confirmDelete = false">Cancel</button>
        </template>
        <button v-else class="danger" @click="confirmDelete = true">Delete client</button>
        <p v-if="deleteError" class="error">{{ deleteError }}</p>
      </div>
    </div>

    <div class="cols">
      <div class="panel">
        <h2>Projects</h2>
        <form class="row" @submit.prevent="addProject">
          <input v-model="pName" placeholder="Project name" required />
          <input v-model.number="pRate" type="number" step="0.01" placeholder="Rate" />
          <button :disabled="createProject.isLoading.value">Add</button>
        </form>
        <p v-if="projError" class="error">{{ projError }}</p>
        <ul>
          <li v-for="p in projects" :key="p.id">
            {{ p.name }} <small>({{ p.billingType }}<span v-if="p.hourlyRate">, {{ p.hourlyRate }}/hr</span>)</small>
          </li>
          <li v-if="projects && projects.length === 0" class="muted">No projects.</li>
        </ul>
      </div>

      <div class="panel">
        <h2>Tasks</h2>
        <form class="row wrap" @submit.prevent="addTask">
          <input v-model="tTitle" placeholder="Task title" required />
          <select v-model="tProjectId">
            <option value="">No project</option>
            <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
          <select v-model="tParentId">
            <option value="">No parent</option>
            <option v-for="t in tasks" :key="t.id" :value="t.id">{{ t.title }}</option>
          </select>
          <input v-model.number="tEstimate" type="number" placeholder="Est. min" />
          <button :disabled="createTask.isLoading.value">Add</button>
        </form>
        <p v-if="taskError" class="error">{{ taskError }}</p>
        <table class="grid">
          <thead><tr><th>Title</th><th>Work</th><th>Billing</th><th>Est.</th></tr></thead>
          <tbody>
            <tr v-for="t in tasks" :key="t.id" :class="{ child: t.parentTaskId }">
              <td>{{ t.parentTaskId ? '↳ ' : '' }}{{ t.title }}</td>
              <td>{{ t.workStatus }}</td>
              <td>{{ t.billingStatus }}</td>
              <td>{{ t.estimatedMinutes ?? '—' }}</td>
            </tr>
            <tr v-if="tasks && tasks.length === 0"><td colspan="4" class="muted">No tasks.</td></tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<style scoped>
.head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-top: 0.25rem; }
.title { display: flex; flex-wrap: wrap; align-items: center; gap: 0.6rem; min-width: 0; flex: 1; }
.title h1 { margin: 0; }
.rename { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
.rename input { min-width: 12rem; font-size: 1.25rem; font-weight: 600; }
button.link { background: none; color: #10b981; padding: 0; }
.danger-zone { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; max-width: 28rem; }
.confirm-text { margin: 0; font-size: 0.9rem; color: #374151; width: 100%; }
.cols { display: grid; grid-template-columns: 1fr 1.4fr; gap: 1.5rem; margin-top: 1rem; }
.panel { border: 1px solid #e5e7eb; border-radius: 10px; padding: 1rem; }
.row { display: flex; gap: 0.5rem; margin-bottom: 0.75rem; }
.row.wrap { flex-wrap: wrap; }
input, select { padding: 0.45rem 0.6rem; border: 1px solid #d1d5db; border-radius: 8px; }
button { padding: 0.45rem 0.8rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
button.secondary { background: #e5e7eb; color: #374151; }
button.danger { background: #dc2626; }
button:disabled { opacity: 0.6; cursor: default; }
ul { list-style: none; padding: 0; margin: 0; }
li { padding: 0.35rem 0; border-bottom: 1px solid #f0f0f0; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.4rem; border-bottom: 1px solid #eee; }
.child td { padding-left: 0.4rem; opacity: 0.85; }
.muted { opacity: 0.6; }
.error { color: #dc2626; }
</style>
