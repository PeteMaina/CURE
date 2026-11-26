import { useState } from 'react';
import { CloseOutlined, SaveOutlined } from '@mui/icons-material';

const initialValues = { name: '', type: 'Business', industry: '', email: '', phone: '', ownerId: '' };

export default function CustomerForm({ customer, onClose, onSubmit, isSubmitting }) {
  const [values, setValues] = useState(customer ? {
    name: customer.name || '', type: customer.type || 'Business', industry: customer.industry || '',
    email: customer.email || '', phone: customer.phone || '', ownerId: customer.ownerId || '',
  } : initialValues);
  const [touched, setTouched] = useState(false);
  const nameError = touched && !values.name.trim() ? 'Enter a customer name.' : '';

  function updateField(event) {
    const { name, value } = event.target;
    setValues((current) => ({ ...current, [name]: value }));
  }

  function submit(event) {
    event.preventDefault();
    setTouched(true);
    if (!values.name.trim()) return;
    onSubmit({ ...values, ownerId: values.ownerId || null, email: values.email || null, phone: values.phone || null });
  }

  return <div className="drawer-layer">
    <button className="drawer-scrim" type="button" onClick={onClose} aria-label="Close customer form" />
    <aside className="form-drawer" role="dialog" aria-modal="true" aria-labelledby="customer-form-title">
      <div className="drawer-header"><div><p className="eyebrow">{customer ? 'Edit record' : 'New record'}</p><h2 id="customer-form-title">{customer ? 'Edit customer' : 'Create customer'}</h2></div><button className="icon-button" type="button" onClick={onClose} aria-label="Close"><CloseOutlined /></button></div>
      <form onSubmit={submit} className="record-form">
        <fieldset><legend>Identity</legend><label>Customer name <span aria-hidden="true">*</span><input name="name" value={values.name} onChange={updateField} onBlur={() => setTouched(true)} aria-invalid={Boolean(nameError)} aria-describedby={nameError ? 'customer-name-error' : undefined} autoFocus />{nameError && <small id="customer-name-error" className="field-error">{nameError}</small>}</label><label>Customer type<select name="type" value={values.type} onChange={updateField}><option>Individual</option><option>Business</option><option>Government</option><option>Non-profit</option><option>Partnership</option><option>Other</option></select></label><label>Industry<input name="industry" value={values.industry} onChange={updateField} /></label></fieldset>
        <fieldset><legend>Primary contact</legend><label>Email<input type="email" name="email" value={values.email} onChange={updateField} /></label><label>Phone<input name="phone" value={values.phone} onChange={updateField} placeholder="+254 ..." /></label></fieldset>
        <fieldset><legend>Ownership</legend><label>Owner ID<input name="ownerId" value={values.ownerId} onChange={updateField} placeholder="Assigned by the API" /></label></fieldset>
        <div className="drawer-actions"><button className="button button-secondary" type="button" onClick={onClose}>Cancel</button><button className="button button-primary" type="submit" disabled={isSubmitting}><SaveOutlined />{isSubmitting ? 'Saving...' : customer ? 'Save changes' : 'Create customer'}</button></div>
      </form>
    </aside>
  </div>;
}
