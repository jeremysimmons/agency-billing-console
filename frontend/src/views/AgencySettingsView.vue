<script setup lang="ts">
import { ref, watch } from 'vue'
import { useAgency, useUpdateAgency } from '../queries/agency'

const { data: agency, isLoading, error } = useAgency()
const updateAgency = useUpdateAgency()

const name = ref('')
const billingEmail = ref('')
const billingAddress = ref('')
const currency = ref('USD')
const paymentTermsDays = ref(30)
const active = ref(true)
const formError = ref('')
const saved = ref(false)

watch(agency, (a) => {
  if (!a) return
  name.value = a.name
  billingEmail.value = a.billingEmail ?? ''
  billingAddress.value = a.billingAddress ?? ''
  currency.value = a.currency
  paymentTermsDays.value = a.paymentTermsDays
  active.value = a.active
}, { immediate: true })

async function save() {
  formError.value = ''
  saved.value = false
  try {
    await updateAgency.mutateAsync({
      name: name.value,
      billingEmail: billingEmail.value || null,
      billingAddress: billingAddress.value || null,
      currency: currency.value,
      paymentTermsDays: paymentTermsDays.value,
      active: active.value,
    })
    saved.value = true
  } catch (e: any) {
    formError.value = e?.response?.data?.error ?? 'Could not save agency.'
  }
}
</script>

<template>
  <section>
    <p class="crumb">Top-level agency</p>
    <h1>Agency settings</h1>
    <p class="lede">Edit the default agency for this deployment.</p>

    <p v-if="isLoading">Loading…</p>
    <p v-else-if="error" class="error">Failed to load agency.</p>
    <form v-else class="form" @submit.prevent="save">
      <label>
        <span>Name</span>
        <input v-model="name" required />
      </label>
      <label>
        <span>Billing email</span>
        <input v-model="billingEmail" type="email" placeholder="billing@example.com" />
      </label>
      <label>
        <span>Billing address</span>
        <textarea v-model="billingAddress" rows="3" placeholder="Street, city, postal code…" />
      </label>
      <div class="row">
        <label>
          <span>Currency</span>
          <input v-model="currency" required maxlength="3" placeholder="USD" />
        </label>
        <label>
          <span>Payment terms (days)</span>
          <input v-model.number="paymentTermsDays" type="number" min="0" required />
        </label>
      </div>
      <label class="check">
        <input v-model="active" type="checkbox" />
        <span>Active</span>
      </label>
      <div class="actions">
        <button type="submit" :disabled="updateAgency.isLoading.value">Save</button>
        <span v-if="saved" class="ok">Saved.</span>
      </div>
      <p v-if="formError" class="error">{{ formError }}</p>
    </form>
  </section>
</template>

<style scoped>
.crumb {
  margin: 0 0 0.25rem;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #6b7280;
}
.lede { margin: 0.35rem 0 1.25rem; color: #4b5563; max-width: 40rem; }
.form { display: flex; flex-direction: column; gap: 0.9rem; max-width: 32rem; }
label { display: flex; flex-direction: column; gap: 0.3rem; font-size: 0.85rem; font-weight: 600; color: #374151; }
label span { font-weight: 600; }
input, textarea {
  padding: 0.5rem 0.7rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font: inherit;
  font-weight: 400;
}
.row { display: flex; gap: 0.75rem; }
.row label { flex: 1; }
.check { flex-direction: row; align-items: center; gap: 0.5rem; font-weight: 500; }
.check input { width: auto; }
.actions { display: flex; align-items: center; gap: 0.75rem; }
button {
  padding: 0.5rem 0.9rem;
  border: none;
  border-radius: 8px;
  background: #10b981;
  color: #fff;
  cursor: pointer;
  font: inherit;
}
button:disabled { opacity: 0.6; cursor: default; }
.ok { color: #059669; font-size: 0.9rem; }
.error { color: #dc2626; }
</style>
