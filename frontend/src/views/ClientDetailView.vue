<script setup lang="ts">
import { ref, computed } from 'vue'
import { useClients } from '../queries/clients'
import { useProjects, useCreateProject } from '../queries/projects'
import { useTasks, useCreateTask } from '../queries/tasks'

const props = defineProps<{ id: string }>()
const clientId = computed(() => props.id)

const { data: clients } = useClients()
const client = computed(() => clients.value?.find((c) => c.id === clientId.value))

const { data: projects } = useProjects(clientId)
const { data: tasks } = useTasks(clientId)
const createProject = useCreateProject()
const createTask = useCreateTask()

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
</script>

<template>
  <section>
    <RouterLink to="/clients">← Clients</RouterLink>
    <h1>{{ client?.name ?? 'Client' }}</h1>

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
.cols { display: grid; grid-template-columns: 1fr 1.4fr; gap: 1.5rem; margin-top: 1rem; }
.panel { border: 1px solid #e5e7eb; border-radius: 10px; padding: 1rem; }
.row { display: flex; gap: 0.5rem; margin-bottom: 0.75rem; }
.row.wrap { flex-wrap: wrap; }
input, select { padding: 0.45rem 0.6rem; border: 1px solid #d1d5db; border-radius: 8px; }
button { padding: 0.45rem 0.8rem; border: none; border-radius: 8px; background: #10b981; color: #fff; cursor: pointer; }
ul { list-style: none; padding: 0; margin: 0; }
li { padding: 0.35rem 0; border-bottom: 1px solid #f0f0f0; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { text-align: left; padding: 0.4rem; border-bottom: 1px solid #eee; }
.child td { padding-left: 0.4rem; opacity: 0.85; }
.muted { opacity: 0.6; }
.error { color: #dc2626; }
</style>
