"use client";

import { useEffect, useState } from "react";
import styles from "./configuracoes.module.css";

const API=process.env.NEXT_PUBLIC_API_URL??"http://localhost:5000";
type RestaurantSettings={id:string;name:string;document?:string;phone?:string;email?:string;street?:string;number?:string;complement?:string;neighborhood?:string;city?:string;state?:string;zipCode?:string;serviceFeePercent:number;openingHours?:string;logoUrl?:string};
type User={id:string;name:string;email:string;role:string;active:boolean};
const roles=["ADMIN","MANAGER","CASHIER","WAITER","KITCHEN"];
const roleLabel=(r:string)=>({ADMIN:"Administrador",MANAGER:"Gerente",CASHIER:"Caixa",WAITER:"Garçom",KITCHEN:"Cozinha"}[r]??r);

export default function ConfiguracoesPage(){
 const[settings,setSettings]=useState<RestaurantSettings|null>(null);const[users,setUsers]=useState<User[]>([]);const[message,setMessage]=useState("");const[error,setError]=useState("");const[loading,setLoading]=useState(true);
 const[newName,setNewName]=useState("");const[newEmail,setNewEmail]=useState("");const[newPassword,setNewPassword]=useState("");const[newRole,setNewRole]=useState("WAITER");
 async function api(path:string,options:RequestInit={}){const token=localStorage.getItem("token");if(!token){location.href="/";throw new Error("Sessão expirada.");}const r=await fetch(`${API}${path}`,{...options,headers:{"Content-Type":"application/json",Authorization:`Bearer ${token}`}});const text=await r.text();if(!r.ok){try{const d=JSON.parse(text);throw new Error(d.message??text)}catch(e){if(e instanceof Error)throw e;throw new Error(text||"Erro na operação.")}}return text?JSON.parse(text):null}
 async function load(){setLoading(true);setError("");try{const[s,u]=await Promise.all([api("/api/restaurant-settings"),api("/api/users")]);setSettings(s);setUsers(u)}catch(e){setError(e instanceof Error?e.message:"Erro ao carregar configurações.")}finally{setLoading(false)}}
 useEffect(()=>{load()},[]);
 function change<K extends keyof RestaurantSettings>(key:K,value:RestaurantSettings[K]){setSettings(s=>s?{...s,[key]:value}:s)}
 async function saveSettings(){if(!settings)return;setMessage("");setError("");try{await api("/api/restaurant-settings",{method:"PUT",body:JSON.stringify(settings)});setMessage("Configurações salvas com sucesso.");await load()}catch(e){setError(e instanceof Error?e.message:"Erro ao salvar configurações.")}}
 async function createUser(){setMessage("");setError("");try{await api("/api/users",{method:"POST",body:JSON.stringify({name:newName,email:newEmail,password:newPassword,role:newRole})});setNewName("");setNewEmail("");setNewPassword("");setNewRole("WAITER");setMessage("Usuário criado com sucesso.");await load()}catch(e){setError(e instanceof Error?e.message:"Erro ao criar usuário.")}}
 async function updateUser(user:User){setMessage("");setError("");try{await api(`/api/users/${user.id}`,{method:"PUT",body:JSON.stringify({name:user.name,role:user.role,active:user.active})});setMessage("Usuário atualizado.");await load()}catch(e){setError(e instanceof Error?e.message:"Erro ao atualizar usuário.")}}
 async function resetPassword(user:User){const value=prompt(`Nova senha para ${user.name}:`,"");if(!value)return;try{await api(`/api/users/${user.id}/reset-password`,{method:"POST",body:JSON.stringify({newPassword:value})});setMessage("Senha redefinida com sucesso.")}catch(e){setError(e instanceof Error?e.message:"Erro ao redefinir senha.")}}
 if(loading)return <main className={styles.page}>Carregando configurações...</main>;
 return <main className={styles.page}>
  <header className={styles.header}><div><a href="/">← Voltar ao painel</a><h1>Configurações</h1><p>Dados do restaurante, usuários e permissões do MVP.</p></div><button className={styles.button} onClick={load}>Atualizar</button></header>
  {message&&<div className={styles.message}>{message}</div>}{error&&<div className={`${styles.message} ${styles.error}`}>{error}</div>}
  {settings&&<section className={styles.panel}><div className={styles.titleRow}><div><h2>Restaurante</h2><p className={styles.muted}>Esses dados pertencem somente ao restaurante da sua sessão.</p></div></div><div className={styles.grid}>
   <label className={styles.field}><span>Nome</span><input value={settings.name??""} onChange={e=>change("name",e.target.value)}/></label>
   <label className={styles.field}><span>CNPJ / Documento</span><input value={settings.document??""} onChange={e=>change("document",e.target.value)}/></label>
   <label className={styles.field}><span>Telefone</span><input value={settings.phone??""} onChange={e=>change("phone",e.target.value)}/></label>
   <label className={styles.field}><span>E-mail</span><input type="email" value={settings.email??""} onChange={e=>change("email",e.target.value)}/></label>
   <label className={styles.field}><span>CEP</span><input value={settings.zipCode??""} onChange={e=>change("zipCode",e.target.value)}/></label>
   <label className={styles.field}><span>Rua / Avenida</span><input value={settings.street??""} onChange={e=>change("street",e.target.value)}/></label>
   <label className={styles.field}><span>Número</span><input value={settings.number??""} onChange={e=>change("number",e.target.value)}/></label>
   <label className={styles.field}><span>Complemento</span><input value={settings.complement??""} onChange={e=>change("complement",e.target.value)}/></label>
   <label className={styles.field}><span>Bairro</span><input value={settings.neighborhood??""} onChange={e=>change("neighborhood",e.target.value)}/></label>
   <label className={styles.field}><span>Cidade</span><input value={settings.city??""} onChange={e=>change("city",e.target.value)}/></label>
   <label className={styles.field}><span>UF</span><input maxLength={2} value={settings.state??""} onChange={e=>change("state",e.target.value.toUpperCase())}/></label>
   <label className={styles.field}><span>Taxa de serviço (%)</span><input type="number" min="0" max="100" step="0.01" value={settings.serviceFeePercent??0} onChange={e=>change("serviceFeePercent",Number(e.target.value))}/></label>
   <label className={styles.field}><span>Logo (URL)</span><input value={settings.logoUrl??""} onChange={e=>change("logoUrl",e.target.value)}/></label>
  </div><div className={styles.grid2}><label className={styles.field}><span>Horário de funcionamento</span><textarea value={settings.openingHours??""} onChange={e=>change("openingHours",e.target.value)} placeholder="Ex.: Seg-Sex 11:00-23:00; Sáb-Dom 12:00-00:00"/></label></div><div className={styles.actions}><button className={styles.button} onClick={saveSettings}>Salvar configurações</button></div></section>}
  <section className={styles.panel}><h2>Usuários e permissões</h2><p className={styles.muted}>Somente administradores podem criar, alterar perfis ou redefinir senhas.</p><div className={styles.grid}>
   <label className={styles.field}><span>Nome</span><input value={newName} onChange={e=>setNewName(e.target.value)} placeholder="Nome do funcionário"/></label>
   <label className={styles.field}><span>E-mail</span><input type="email" value={newEmail} onChange={e=>setNewEmail(e.target.value)} placeholder="usuario@restaurante.com"/></label>
   <label className={styles.field}><span>Senha inicial</span><input type="password" value={newPassword} onChange={e=>setNewPassword(e.target.value)} placeholder="Mínimo 8 caracteres"/></label>
   <label className={styles.field}><span>Perfil</span><select value={newRole} onChange={e=>setNewRole(e.target.value)}>{roles.map(r=><option key={r} value={r}>{roleLabel(r)}</option>)}</select></label>
  </div><div className={styles.actions}><button className={styles.button} onClick={createUser}>Criar usuário</button></div>
  <div className={styles.users}>{users.map((u,index)=><div className={styles.user} key={u.id}><div><input value={u.name} onChange={e=>setUsers(v=>v.map((x,i)=>i===index?{...x,name:e.target.value}:x))}/><small>{u.email}</small></div><select value={u.role} onChange={e=>setUsers(v=>v.map((x,i)=>i===index?{...x,role:e.target.value}:x))}>{roles.map(r=><option key={r} value={r}>{roleLabel(r)}</option>)}</select><label><input type="checkbox" checked={u.active} onChange={e=>setUsers(v=>v.map((x,i)=>i===index?{...x,active:e.target.checked}:x))}/> Ativo</label><div className={styles.passwordBox}><button className={`${styles.button} ${styles.secondary}`} onClick={()=>resetPassword(u)}>Senha</button><button className={styles.button} onClick={()=>updateUser(u)}>Salvar</button></div></div>)}</div></section>
 </main>;
}
