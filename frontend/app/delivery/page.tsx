"use client";

import { useEffect, useMemo, useState } from "react";
import styles from "./delivery.module.css";

const API=process.env.NEXT_PUBLIC_API_URL??"http://localhost:5000";
type Product={id:string;name:string;price:number};
type Customer={id:string;name:string;phone?:string;cashbackBalance:number};
type Zone={id:string;name:string;fee:number;estimatedMinutes:number;active:boolean};
type Driver={id:string;name:string;phone?:string;vehicle?:string;active:boolean};
type Delivery={id:string;orderId:string;zoneName:string;driverId?:string;driverName?:string;address:string;number?:string;neighborhood:string;deliveryFee:number;status:string;customerName?:string;orderTotal:number;orderStatus:string;createdAt:string};
type CartItem={productId:string;quantity:number;notes:string};

const money=(v?:number|null)=>(typeof v==="number"?v:0).toLocaleString("pt-BR",{style:"currency",currency:"BRL"});
const orderLabel=(s:string)=>({NEW:"Novo",PREPARING:"Em preparo",READY:"Pronto",DELIVERED:"Entregue",CLOSED:"Pago",CANCELLED:"Cancelado"}[s]??s);
const deliveryLabel=(s:string)=>({WAITING:"Aguardando",OUT_FOR_DELIVERY:"Em rota",DELIVERED:"Entregue"}[s]??s);

export default function DeliveryPage(){
 const[products,setProducts]=useState<Product[]>([]);const[customers,setCustomers]=useState<Customer[]>([]);const[zones,setZones]=useState<Zone[]>([]);const[drivers,setDrivers]=useState<Driver[]>([]);const[deliveries,setDeliveries]=useState<Delivery[]>([]);
 const[customerId,setCustomerId]=useState("");const[customerName,setCustomerName]=useState("");const[zoneId,setZoneId]=useState("");const[address,setAddress]=useState("");const[number,setNumber]=useState("");const[complement,setComplement]=useState("");const[neighborhood,setNeighborhood]=useState("");const[reference,setReference]=useState("");
 const[cart,setCart]=useState<CartItem[]>([]);const[message,setMessage]=useState("");const[loading,setLoading]=useState(true);const[creating,setCreating]=useState(false);
 const[zoneName,setZoneName]=useState("");const[zoneFee,setZoneFee]=useState("");const[zoneMinutes,setZoneMinutes]=useState("45");
 const[driverName,setDriverName]=useState("");const[driverPhone,setDriverPhone]=useState("");const[driverVehicle,setDriverVehicle]=useState("");

 async function api(path:string,options:RequestInit={}){
  const token=localStorage.getItem("token");if(!token){location.href="/";throw new Error("Sessão expirada.");}
  const r=await fetch(`${API}${path}`,{...options,headers:{"Content-Type":"application/json",Authorization:`Bearer ${token}`}});
  const text=await r.text();
  if(!r.ok){try{const d=JSON.parse(text);throw new Error(d.detail?`${d.message} ${d.detail}`:(d.message??text))}catch(e){if(e instanceof Error)throw e;throw new Error(text||"Erro na operação.")}}
  return text?JSON.parse(text):null;
 }

 async function load(){setLoading(true);try{const[p,c,z,d,o]=await Promise.all([api("/api/products"),api("/api/customers"),api("/api/delivery/zones"),api("/api/delivery/drivers"),api("/api/delivery/orders")]);setProducts(p);setCustomers(c);setZones(z);setDrivers(d);setDeliveries(o);if(!zoneId){const first=z.find((x:Zone)=>x.active);if(first)setZoneId(first.id)}}catch(e){setMessage(e instanceof Error?e.message:"Erro ao carregar delivery.")}finally{setLoading(false)}}
 useEffect(()=>{load()},[]);

 const selectedZone=zones.find(z=>z.id===zoneId);const subtotal=useMemo(()=>cart.reduce((s,i)=>s+(products.find(p=>p.id===i.productId)?.price??0)*i.quantity,0),[cart,products]);const total=subtotal+(selectedZone?.fee??0);
 function addItem(){if(products.length)setCart([...cart,{productId:products[0].id,quantity:1,notes:""}])}
 function updateItem(index:number,field:keyof CartItem,value:string){setCart(cart.map((x,i)=>i===index?{...x,[field]:field==="quantity"?Number(value):value}:x) as CartItem[])}

 async function createDelivery(){
  if(creating)return;
  if(!cart.length||!zoneId||!address.trim()||!neighborhood.trim()){setMessage("Informe região, endereço, bairro e pelo menos um item.");return;}
  setCreating(true);setMessage("");
  try{
   const result=await api("/api/delivery/create",{method:"POST",body:JSON.stringify({customerId:customerId||null,customerName:customerId?null:(customerName||null),zoneId,address:address.trim(),number:number||null,complement:complement||null,neighborhood:neighborhood.trim(),reference:reference||null,items:cart})});
   setCart([]);setCustomerId("");setCustomerName("");setAddress("");setNumber("");setComplement("");setNeighborhood("");setReference("");
   setMessage(`Delivery criado com sucesso. Total: ${money(result.total)}`);await load();
  }catch(e){setMessage(e instanceof Error?e.message:"Erro ao criar delivery.")}finally{setCreating(false)}
 }

 async function createZone(){try{await api("/api/delivery/zones",{method:"POST",body:JSON.stringify({name:zoneName,fee:Number(zoneFee||0),estimatedMinutes:Number(zoneMinutes||45)})});setZoneName("");setZoneFee("");setMessage("Região criada.");await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro ao criar região.")}}
 async function createDriver(){try{await api("/api/delivery/drivers",{method:"POST",body:JSON.stringify({name:driverName,phone:driverPhone||null,vehicle:driverVehicle||null})});setDriverName("");setDriverPhone("");setDriverVehicle("");setMessage("Entregador cadastrado.");await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro ao cadastrar entregador.")}}
 async function assignDriver(deliveryId:string,driverId:string){if(!driverId)return;try{await api(`/api/delivery/orders/${deliveryId}/driver`,{method:"PATCH",body:JSON.stringify({driverId})});await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro ao atribuir entregador.")}}
 async function advanceKitchen(d:Delivery){const next=d.orderStatus==="NEW"?"PREPARING":d.orderStatus==="PREPARING"?"READY":null;if(!next)return;try{await api(`/api/orders/${d.orderId}/status`,{method:"PATCH",body:JSON.stringify({status:next})});await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro ao atualizar cozinha.")}}
 async function deliveryStatus(id:string,status:string){try{await api(`/api/delivery/orders/${id}/status`,{method:"PATCH",body:JSON.stringify({status})});await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro ao atualizar entrega.")}}
 async function pay(d:Delivery,method:string){const coupon=prompt("Cupom (opcional):","")??"";const cashback=prompt("Cashback a utilizar (opcional):","0")??"0";try{await api(`/api/orders/${d.orderId}/close`,{method:"POST",body:JSON.stringify({paymentMethod:method,couponCode:coupon||null,cashbackAmount:Number(cashback||0)})});setMessage("Pagamento concluído.");await load()}catch(e){setMessage(e instanceof Error?e.message:"Erro no pagamento.")}}

 if(loading)return <main className={styles.page}>Carregando delivery...</main>;
 return <main className={styles.page}>
  <header><div><a href="/">← Voltar ao painel</a><h1>Delivery</h1><p>Pedidos, regiões, taxas e entregadores.</p></div><button onClick={load}>Atualizar</button></header>
  {message&&<div className={styles.message}>{message}</div>}

  <section className={styles.panel}><h2>Novo pedido delivery</h2><div className={styles.formGrid}>
   <select value={customerId} onChange={e=>{setCustomerId(e.target.value);if(e.target.value)setCustomerName("")}}><option value="">Cliente avulso</option>{customers.map(c=><option key={c.id} value={c.id}>{c.name}{c.phone?` · ${c.phone}`:""} · cashback {money(c.cashbackBalance)}</option>)}</select>
   <input value={customerName} disabled={!!customerId} onChange={e=>setCustomerName(e.target.value)} placeholder="Nome avulso"/>
   <select value={zoneId} onChange={e=>setZoneId(e.target.value)}><option value="">Região</option>{zones.filter(z=>z.active).map(z=><option key={z.id} value={z.id}>{z.name} · {money(z.fee)} · ~{z.estimatedMinutes} min</option>)}</select>
   <input value={address} onChange={e=>setAddress(e.target.value)} placeholder="Rua / avenida"/><input value={number} onChange={e=>setNumber(e.target.value)} placeholder="Número"/><input value={neighborhood} onChange={e=>setNeighborhood(e.target.value)} placeholder="Bairro"/><input value={complement} onChange={e=>setComplement(e.target.value)} placeholder="Complemento"/><input value={reference} onChange={e=>setReference(e.target.value)} placeholder="Referência"/>
  </div>
  <div className={styles.items}>{cart.map((i,index)=>{const p=products.find(x=>x.id===i.productId);return <div className={styles.item} key={index}><select value={i.productId} onChange={e=>updateItem(index,"productId",e.target.value)}>{products.map(p=><option key={p.id} value={p.id}>{p.name} · {money(p.price)}</option>)}</select><input type="number" min="1" value={i.quantity} onChange={e=>updateItem(index,"quantity",e.target.value)}/><input value={i.notes} onChange={e=>updateItem(index,"notes",e.target.value)} placeholder="Observação"/><strong>{money((p?.price??0)*i.quantity)}</strong><button className={styles.danger} onClick={()=>setCart(cart.filter((_,x)=>x!==index))}>Remover</button></div>})}</div>
  <div className={styles.footer}><button onClick={addItem}>+ Item</button><div><span>Produtos {money(subtotal)} · Taxa {money(selectedZone?.fee)}</span><strong>Total {money(total)}</strong><button disabled={creating} onClick={createDelivery}>{creating?"Criando...":"Criar delivery"}</button></div></div></section>

  <section className={styles.grid}><div className={styles.panel}><h2>Regiões e taxas</h2><div className={styles.inline}><input value={zoneName} onChange={e=>setZoneName(e.target.value)} placeholder="Região / bairro"/><input type="number" step="0.01" value={zoneFee} onChange={e=>setZoneFee(e.target.value)} placeholder="Taxa"/><input type="number" value={zoneMinutes} onChange={e=>setZoneMinutes(e.target.value)} placeholder="Minutos"/><button onClick={createZone}>Adicionar</button></div>{zones.map(z=><div className={styles.row} key={z.id}><div><strong>{z.name}</strong><small>{money(z.fee)} · ~{z.estimatedMinutes} min</small></div><button onClick={()=>api(`/api/delivery/zones/${z.id}/toggle`,{method:"PATCH"}).then(load)}>{z.active?"Ativa":"Inativa"}</button></div>)}</div>
  <div className={styles.panel}><h2>Entregadores</h2><div className={styles.inline}><input value={driverName} onChange={e=>setDriverName(e.target.value)} placeholder="Nome"/><input value={driverPhone} onChange={e=>setDriverPhone(e.target.value)} placeholder="Telefone"/><input value={driverVehicle} onChange={e=>setDriverVehicle(e.target.value)} placeholder="Veículo"/><button onClick={createDriver}>Adicionar</button></div>{drivers.map(d=><div className={styles.row} key={d.id}><div><strong>{d.name}</strong><small>{d.phone||"Sem telefone"} · {d.vehicle||"Veículo não informado"}</small></div><button onClick={()=>api(`/api/delivery/drivers/${d.id}/toggle`,{method:"PATCH"}).then(load)}>{d.active?"Ativo":"Inativo"}</button></div>)}</div></section>

  <section className={styles.panel}><h2>Entregas</h2><div className={styles.cards}>{deliveries.length?deliveries.map(d=><article className={styles.card} key={d.id}><div className={styles.cardHead}><div><strong>{d.customerName||"Cliente avulso"}</strong><small>{d.address}{d.number?`, ${d.number}`:""} · {d.neighborhood}</small></div><span>{deliveryLabel(d.status)}</span></div><div className={styles.meta}><span>Cozinha: <b>{orderLabel(d.orderStatus)}</b></span><span>Região: <b>{d.zoneName}</b></span><span>Taxa: <b>{money(d.deliveryFee)}</b></span><span>Total: <b>{money(d.orderTotal)}</b></span></div>{d.status==="WAITING"&&<select value={d.driverId??""} onChange={e=>assignDriver(d.id,e.target.value)}><option value="">Selecionar entregador</option>{drivers.filter(x=>x.active).map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select>}<div className={styles.actions}>{(d.orderStatus==="NEW"||d.orderStatus==="PREPARING")&&<button onClick={()=>advanceKitchen(d)}>{d.orderStatus==="NEW"?"Iniciar preparo":"Marcar pronto"}</button>}{d.orderStatus==="READY"&&d.status==="WAITING"&&<button onClick={()=>deliveryStatus(d.id,"OUT_FOR_DELIVERY")}>Sair para entrega</button>}{d.status==="OUT_FOR_DELIVERY"&&<button onClick={()=>deliveryStatus(d.id,"DELIVERED")}>Confirmar entrega</button>}{d.orderStatus==="DELIVERED"&&<><button onClick={()=>pay(d,"PIX")}>Pix</button><button onClick={()=>pay(d,"CARD")}>Cartão</button><button onClick={()=>pay(d,"CASH")}>Dinheiro</button></>}{d.orderStatus==="CLOSED"&&<strong>Pagamento concluído</strong>}</div></article>):<p>Nenhum delivery cadastrado.</p>}</div></section>
 </main>;
}
