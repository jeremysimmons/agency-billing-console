import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/', redirect: '/tasks' },
  { path: '/tasks', name: 'tasks', component: () => import('./views/TasksView.vue') },
  { path: '/clients', name: 'clients', component: () => import('./views/ClientsView.vue') },
  { path: '/clients/:id', name: 'client-detail', component: () => import('./views/ClientDetailView.vue'), props: true },
  { path: '/projects', name: 'projects', component: () => import('./views/ProjectsView.vue') },
  { path: '/invoices', name: 'invoices', component: () => import('./views/InvoicesView.vue') },
  { path: '/hierarchy', name: 'hierarchy', component: () => import('./views/HierarchyView.vue') },
  { path: '/sync', name: 'sync', component: () => import('./views/SyncView.vue') },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})
