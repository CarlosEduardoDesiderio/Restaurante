"use client";
import { useEffect, useMemo, useState } from "react";

const API=process.env.NEXT_PUBLIC_API_URL??"http://localhost:5000";
type Summary={revenue:number;orders:number;averageTicket:number;criticalStock:number;openOrders:number};
type Product={id:string;name:string;description?:string;price:number};
type Ingredient={id:string;name:string;unit:string;currentQuantity:number;minimumQuantity:number;costPerUnit:number};
type Table={id:string;number:number;seats:number;status:string};
type RecipeItem={ingredientId:string;name:string;unit:string;quantity:number;costPerUnit:number};
type RecipeResponse={id:string;name:string;price:number;ingredients:RecipeItem[];cost:number};
type StockMovement={id:string;ingredientId:string;orderId?:string;type:string;quantity:number;description:string;createdAt:string};

function money(v:number){return v.toLocaleString("pt-BR",{style:"currency",currency:"BRL"})}

export default function Home(){
 const[token,setToken]=useState<string|null>(null);
 const[email,setEmail]=useState("admin@demo.local");
 const[password,setPassword]=useState("Admin@123");
 const[summary,setSummary]=useState<Summary|null>(null);
 const[products,setProducts]=useState<Product[]>([]);
 const[stock,setStock]=useState<Ingredient[]>([]);
 const[tables,setTables]=useState<Table[]>([]);
 const[q,setQ]=useState("");
 const[answer,setAnswer]=useState("");
 const[tab,setTab]=useState("dashboard");
 const[loginError,setLoginError]=useState("");
 const[productName,setProductName]=useState("");
 const[productPrice,setProductPrice]=useState("");
 const[ingredientName,setIngredientName]=useState("");
 const[selectedProductId,setSelectedProductId]=useState("");
 const[recipeItems,setRecipeItems]=useState<{ingredientId:string;quantity:number}[]>([]);
 const[recipeMessage,setRecipeMessage]=useState("");
 const[selectedStockId,setSelectedStockId]=useState("");
 const[entryQuantity,setEntryQuantity]=useState("");
 const[entryDescription,setEntryDescription]=useState("");
 const[movements,setMovements]=useState<StockMovement[]>([]);
 const[stockMessage,setStockMessage]=useState("");

 async function api(path:string,options:RequestInit={}){
  const r=await fetch(`${API}${path}`,{...options,headers:{"Content-Type":"application/json",...(token?{Authorization:`Bearer ${token}`}:{})}});
  if(!r.ok)throw new Error(await r.text());
  if(r.status===204)return null;
  const text=await r.text();
  return text?JSON.parse(text):null;
 }
 async function login(){try{const d=await api("/api/auth/login",{method:"POST",body:JSON.stringify({email,password})});localStorage.setItem("token",d.token);setToken(d.token);setLoginError("")}catch{setLoginError("E-mail ou senha inválidos.")}}
 async function load(){if(!token)return;try{const[s,p,i,t]=await Promise.all([api("/api/dashboard/summary"),api("/api/products"),api("/api/stock"),api("/api/tables")]);setSummary(s);setProducts(p);setStock(i);setTables(t)}catch{localStorage.removeItem("token");setToken(null)}}
 useEffect(()=>{const t=localStorage.getItem("token");if(t)setToken(t)},[]);
 useEffect(()=>{load()},[token]);

 async function addProduct(){if(!productName||!productPrice)return;await api("/api/products",{method:"POST",body:JSON.stringify({name:productName,price:Number(productPrice),active:true})});setProductName("");setProductPrice("");load()}
 async function addIngredient(){if(!ingredientName)return;await api("/api/stock",{method:"POST",body:JSON.stringify({name:ingredientName,unit:"un",currentQuantity:0,minimumQuantity:0,costPerUnit:0})});setIngredientName("");load()}
 async function ask(){if(!q)return;const d=await api("/api/ai/ask",{method:"POST",body:JSON.stringify({question:q})});setAnswer(d.answer)}

 async function loadRecipe(productId:string){
  setSelectedProductId(productId);setRecipeMessage("");
  if(!productId){setRecipeItems([]);return;}
  try{const d:RecipeResponse=await api(`/api/recipes/${productId}`);setRecipeItems((d.ingredients??[]).map(x=>({ingredientId:x.ingredientId,quantity:x.quantity})))}catch{setRecipeItems([])}
 }
 function addRecipeItem(){const first=stock.find(i=>!recipeItems.some(r=>r.ingredientId===i.id));if(!first)return;setRecipeItems([...recipeItems,{ingredientId:first.id,quantity:1}])}
 function updateRecipeItem(index:number,field:"ingredientId"|"quantity",value:string){setRecipeItems(recipeItems.map((x,i)=>i===index?{...x,[field]:field==="quantity"?Number(value):value}:x))}
 function removeRecipeItem(index:number){setRecipeItems(recipeItems.filter((_,i)=>i!==index))}
 async function saveRecipe(){
  if(!selectedProductId)return;
  try{await api(`/api/recipes/${selectedProductId}`,{method:"PUT",body:JSON.stringify({items:recipeItems})});setRecipeMessage("Ficha técnica salva com sucesso.");await loadRecipe(selectedProductId)}
  catch(e){setRecipeMessage(e instanceof Error?e.message:"Erro ao salvar ficha técnica.")}
 }
 const selectedProduct=products.find(p=>p.id===selectedProductId);
 const recipeCost=useMemo(()=>recipeItems.reduce((sum,r)=>{const i=stock.find(s=>s.id===r.ingredientId);return sum+(i?.costPerUnit??0)*Number(r.quantity||0)},0),[recipeItems,stock]);
 const recipeMargin=(selectedProduct?.price??0)-recipeCost;

 async function addStockEntry(){
  if(!selectedStockId||Number(entryQuantity)<=0)return;
  try{await api(`/api/stock/${selectedStockId}/entry`,{method:"POST",body:JSON.stringify({quantity:Number(entryQuantity),description:entryDescription||null})});setEntryQuantity("");setEntryDescription("");setStockMessage("Entrada registrada com sucesso.");await load();await loadMovements()}
  catch(e){setStockMessage(e instanceof Error?e.message:"Erro ao registrar entrada.")}
 }
 async function loadMovements(){try{const suffix=selectedStockId?`?ingredientId=${selectedStockId}`:"";setMovements(await api(`/api/stock/movements${suffix}`))}catch{setMovements([])}}
 useEffect(()=>{if(token&&tab==="estoque")loadMovements()},[tab,selectedStockId,token]);

 if(!token)return <main className="login"><div className="loginCard"><h1>🍽️ Restaurante Inteligente</h1><p>Gestão completa para sua operação</p><input value={email} onChange={e=>setEmail(e.target.value)} placeholder="E-mail"/><input type="password" value={password} onChange={e=>setPassword(e.target.value)} placeholder="Senha"/><button onClick={login}>Entrar</button>{loginError&&<p className="error">{loginError}</p>}<small>Demo: admin@demo.local / Admin@123</small></div></main>;

 return <div className="app"><aside><h2>🍽️ RestoIA</h2>{["dashboard","produtos","ficha técnica","estoque","mesas","pedidos","ia"].map(x=><button className={tab===x?"active":""} onClick={()=>setTab(x)} key={x}>{x[0].toUpperCase()+x.slice(1)}</button>)}<button onClick={()=>{localStorage.removeItem("token");setToken(null)}}>Sair</button></aside><main className="content"><header><div><h1>{tab[0].toUpperCase()+tab.slice(1)}</h1><p>Painel de gestão do restaurante</p></div><button onClick={load}>↻ Atualizar</button></header>
 {tab==="dashboard"&&<><section className="cards"><Card title="Faturamento hoje" value={summary?money(summary.revenue):"—"}/><Card title="Pedidos hoje" value={summary?.orders??"—"}/><Card title="Ticket médio" value={summary?money(summary.averageTicket):"—"}/><Card title="Estoque crítico" value={summary?.criticalStock??"—"}/><Card title="Pedidos abertos" value={summary?.openOrders??"—"}/></section><section className="panel"><h2>Assistente Inteligente</h2><div className="ask"><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Ex.: Qual foi meu faturamento hoje?"/><button onClick={ask}>Perguntar</button></div>{answer&&<div className="answer">{answer}</div>}</section></>}
 {tab==="produtos"&&<Panel title="Produtos"><div className="form"><input value={productName} onChange={e=>setProductName(e.target.value)} placeholder="Nome do produto"/><input value={productPrice} onChange={e=>setProductPrice(e.target.value)} placeholder="Preço" type="number"/><button onClick={addProduct}>Adicionar</button></div><div className="dataList">{products.length?products.map(p=><div className="dataRow" key={p.id}><div><strong>{p.name}</strong><small>{p.description||"Sem descrição"}</small></div><b>{money(p.price)}</b><button onClick={()=>{setTab("ficha técnica");loadRecipe(p.id)}}>Ficha técnica</button></div>):<p>Nenhum produto cadastrado.</p>}</div></Panel>}
 {tab==="ficha técnica"&&<Panel title="Ficha Técnica"><div className="recipeTop"><label>Produto<select value={selectedProductId} onChange={e=>loadRecipe(e.target.value)}><option value="">Selecione um produto</option>{products.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></label>{selectedProduct&&<div className="recipeSummary"><span>Preço <strong>{money(selectedProduct.price)}</strong></span><span>Custo <strong>{money(recipeCost)}</strong></span><span>Margem bruta <strong>{money(recipeMargin)}</strong></span></div>}</div>{selectedProductId&&<><div className="recipeTable"><div className="recipeHead"><span>Ingrediente</span><span>Quantidade</span><span>Unidade</span><span></span></div>{recipeItems.map((r,index)=>{const ingredient=stock.find(i=>i.id===r.ingredientId);return <div className="recipeRow" key={`${r.ingredientId}-${index}`}><select value={r.ingredientId} onChange={e=>updateRecipeItem(index,"ingredientId",e.target.value)}>{stock.map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select><input type="number" min="0.001" step="0.001" value={r.quantity} onChange={e=>updateRecipeItem(index,"quantity",e.target.value)}/><span>{ingredient?.unit??"-"}</span><button className="dangerBtn" onClick={()=>removeRecipeItem(index)}>Remover</button></div>})}</div><div className="recipeActions"><button className="secondaryBtn" onClick={addRecipeItem}>+ Ingrediente</button><button onClick={saveRecipe}>Salvar ficha técnica</button></div>{recipeMessage&&<p className={recipeMessage.includes("sucesso")?"success":"error"}>{recipeMessage}</p>}</>}</Panel>}
 {tab==="estoque"&&<><Panel title="Estoque"><div className="form"><input value={ingredientName} onChange={e=>setIngredientName(e.target.value)} placeholder="Ingrediente"/><button onClick={addIngredient}>Adicionar</button></div><div className="dataList">{stock.length?stock.map(i=><div className="dataRow" key={i.id}><div><strong>{i.name}</strong><small>Mínimo: {i.minimumQuantity} {i.unit} · Custo: {money(i.costPerUnit)}</small></div><b className={i.currentQuantity<=i.minimumQuantity?"critical":""}>{i.currentQuantity} {i.unit}</b><button onClick={()=>setSelectedStockId(i.id)}>Movimentar</button></div>):<p>Nenhum ingrediente cadastrado.</p>}</div></Panel><Panel title="Entrada e Histórico"><div className="stockEntry"><select value={selectedStockId} onChange={e=>setSelectedStockId(e.target.value)}><option value="">Todos os ingredientes</option>{stock.map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select><input type="number" min="0.001" step="0.001" value={entryQuantity} onChange={e=>setEntryQuantity(e.target.value)} placeholder="Quantidade de entrada"/><input value={entryDescription} onChange={e=>setEntryDescription(e.target.value)} placeholder="Descrição (opcional)"/><button disabled={!selectedStockId} onClick={addStockEntry}>Registrar entrada</button></div>{stockMessage&&<p className={stockMessage.includes("sucesso")?"success":"error"}>{stockMessage}</p>}<div className="movementList">{movements.length?movements.map(m=>{const ing=stock.find(i=>i.id===m.ingredientId);return <div className="movementRow" key={m.id}><span className={`movementType ${m.type.toLowerCase()}`}>{m.type}</span><div><strong>{ing?.name??m.ingredientId}</strong><small>{m.description}</small></div><b>{m.quantity} {ing?.unit??""}</b><small>{new Date(m.createdAt).toLocaleString("pt-BR")}</small></div>}):<p>Nenhuma movimentação encontrada.</p>}</div></Panel></>}
 {tab==="mesas"&&<Panel title="Mapa de mesas"><div className="tables">{tables.map(t=><div className={`table ${t.status.toLowerCase()}`} key={t.id}><strong>Mesa {t.number}</strong><span>{t.status}</span></div>)}</div></Panel>}
 {tab==="pedidos"&&<Panel title="Pedidos"><p>O backend já cria pedidos com validação e baixa automática do estoque. A próxima etapa será a tela operacional e o KDS da cozinha.</p></Panel>}
 {tab==="ia"&&<Panel title="Assistente IA"><div className="ask"><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Pergunte sobre vendas ou estoque"/><button onClick={ask}>Analisar</button></div>{answer&&<div className="answer">{answer}</div>}<p>Azure OpenAI fica preparado para análises avançadas depois que as regras de consulta estiverem configuradas.</p></Panel>}
 </main></div>
}

function Card({title,value}:{title:string;value:string|number}){return <div className="card"><span>{title}</span><strong>{value}</strong></div>}
function Panel({title,children}:{title:string;children:React.ReactNode}){return <section className="panel"><h2>{title}</h2>{children}</section>}
