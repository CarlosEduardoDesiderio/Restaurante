"use client";

import { useEffect, useState } from "react";
import styles from "./financeiro.module.css";

const API=process.env.NEXT_PUBLIC_API_URL??"http://localhost:5000";
type Summary={revenue:number;orders:number;paidOrders:number;averageTicket:number;criticalStock:number;openOrders:number;payments:{pix:number;card:number;cash:number};cmv:number;cmvPercentage:number;grossProfit:number;grossMarginPercentage:number;topProducts:{productId:string;name:string;quantity:number;revenue:number}[]};
const money=(v?:number|null)=>(typeof v==="number"?v:0).toLocaleString("pt-BR",{style:"currency",currency:"BRL"});
const pct=(v?:number|null)=>`${(typeof v==="number"?v:0).toFixed(1)}%`;

export default function FinanceiroPage(){
 const[data,setData]=useState<Summary|null>(null);const[error,setError]=useState("");const[loading,setLoading]=useState(true);
 async function load(){setLoading(true);setError("");try{const token=localStorage.getItem("token");if(!token){location.href="/";return;}const r=await fetch(`${API}/api/dashboard/summary`,{headers:{Authorization:`Bearer ${token}`}});if(!r.ok)throw new Error(await r.text());setData(await r.json())}catch(e){setError(e instanceof Error?e.message:"Erro ao carregar financeiro.")}finally{setLoading(false)}}
 useEffect(()=>{load()},[]);
 if(loading)return <main className={styles.page}>Carregando financeiro...</main>;
 return <main className={styles.page}><header><div><a href="/">← Voltar ao painel</a><h1>Dashboard Financeiro</h1><p>Indicadores reais das vendas fechadas de hoje.</p></div><button onClick={load}>Atualizar</button></header>{error&&<div className={styles.error}>{error}</div>}{data&&<><section className={styles.cards}><Metric label="Faturamento" value={money(data.revenue)}/><Metric label="Pedidos pagos" value={String(data.paidOrders)}/><Metric label="Ticket médio" value={money(data.averageTicket)}/><Metric label="CMV" value={money(data.cmv)} sub={pct(data.cmvPercentage)}/><Metric label="Lucro bruto" value={money(data.grossProfit)}/><Metric label="Margem bruta" value={pct(data.grossMarginPercentage)}/><Metric label="Estoque crítico" value={String(data.criticalStock)}/><Metric label="Pedidos ativos" value={String(data.openOrders)}/></section><section className={styles.panel}><h2>Formas de pagamento</h2><div className={styles.payments}><Metric label="Pix" value={money(data.payments?.pix)}/><Metric label="Cartão" value={money(data.payments?.card)}/><Metric label="Dinheiro" value={money(data.payments?.cash)}/></div></section><section className={styles.panel}><h2>Produtos mais vendidos hoje</h2>{data.topProducts?.length?<div className={styles.list}>{data.topProducts.map((p,i)=><div className={styles.row} key={p.productId}><div><strong>{i+1}. {p.name}</strong><small>{p.quantity} unidades</small></div><strong>{money(p.revenue)}</strong></div>)}</div>:<p>Nenhuma venda fechada hoje.</p>}</section><section className={styles.note}><strong>Como calculamos:</strong> faturamento considera pedidos fechados hoje; CMV usa a ficha técnica × custo atual dos ingredientes; lucro bruto = faturamento − CMV.</section></>}</main>
}
function Metric({label,value,sub}:{label:string;value:string;sub?:string}){return <div className={styles.metric}><span>{label}</span><strong>{value}</strong>{sub&&<small>{sub}</small>}</div>}
