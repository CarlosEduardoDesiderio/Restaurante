"use client";

import { useEffect, useMemo, useState } from "react";
import styles from "./caixa.module.css";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

type CashSummary = {
  open: boolean;
  id?: string;
  status?: string;
  openedAt?: string;
  openingAmount?: number;
  expectedCash?: number;
  pix?: number;
  card?: number;
  cashSales?: number;
  totalSales?: number;
  supplies?: number;
  withdrawals?: number;
  movements?: number;
};

type CashMovement = {
  id: string;
  type: string;
  description: string;
  amount: number;
  paymentMethod?: string;
  createdAt: string;
};

type CashHistory = {
  id: string;
  status: string;
  openingAmount: number;
  expectedCashAmount?: number;
  countedCashAmount?: number;
  differenceAmount?: number;
  openedAt: string;
  closedAt?: string;
};

function money(value = 0) {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function CashPage() {
  const [token, setToken] = useState<string | null>(null);
  const [summary, setSummary] = useState<CashSummary>({ open: false });
  const [movements, setMovements] = useState<CashMovement[]>([]);
  const [history, setHistory] = useState<CashHistory[]>([]);
  const [openingAmount, setOpeningAmount] = useState("100");
  const [operationAmount, setOperationAmount] = useState("");
  const [description, setDescription] = useState("");
  const [countedCash, setCountedCash] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setToken(localStorage.getItem("token"));
  }, []);

  async function api(path: string, options: RequestInit = {}) {
    const authToken = token ?? localStorage.getItem("token");
    const response = await fetch(`${API}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
      },
    });

    const text = await response.text();
    if (!response.ok) {
      try {
        const data = JSON.parse(text);
        throw new Error(data.message ?? text);
      } catch {
        throw new Error(text || "Erro ao executar operação.");
      }
    }
    return text ? JSON.parse(text) : null;
  }

  async function load() {
    const authToken = token ?? localStorage.getItem("token");
    if (!authToken) return;
    try {
      const current: CashSummary = await api("/api/cash/current");
      setSummary(current);
      setHistory(await api("/api/cash/history"));
      if (current.open && current.id) {
        setMovements(await api(`/api/cash/${current.id}/movements`));
        setCountedCash(String(current.expectedCash ?? 0));
      } else {
        setMovements([]);
      }
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Erro ao carregar caixa.");
    }
  }

  useEffect(() => {
    if (token) load();
  }, [token]);

  async function execute(action: () => Promise<unknown>, success: string) {
    setLoading(true);
    setMessage("");
    try {
      await action();
      setMessage(success);
      setOperationAmount("");
      setDescription("");
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Erro na operação.");
    } finally {
      setLoading(false);
    }
  }

  const differencePreview = useMemo(() => {
    if (!summary.open || countedCash === "") return null;
    return Number(countedCash) - (summary.expectedCash ?? 0);
  }, [countedCash, summary]);

  if (token === null) {
    return <main className={styles.center}>Carregando...</main>;
  }

  if (!token) {
    return (
      <main className={styles.center}>
        <div className={styles.authCard}>
          <h1>Caixa</h1>
          <p>Entre no sistema primeiro para acessar o PDV.</p>
          <a href="/">Ir para o login</a>
        </div>
      </main>
    );
  }

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <div>
          <a href="/" className={styles.back}>← Voltar ao painel</a>
          <h1>PDV · Caixa</h1>
          <p>Abertura, sangria, suprimento e fechamento do caixa.</p>
        </div>
        <button onClick={load} disabled={loading}>Atualizar</button>
      </header>

      {!summary.open ? (
        <section className={styles.openCard}>
          <div>
            <span className={styles.statusClosed}>CAIXA FECHADO</span>
            <h2>Abrir caixa</h2>
            <p>Informe o fundo de troco disponível no início do expediente.</p>
          </div>
          <div className={styles.openForm}>
            <label>
              Fundo de troco
              <input type="number" min="0" step="0.01" value={openingAmount} onChange={e => setOpeningAmount(e.target.value)} />
            </label>
            <button disabled={loading} onClick={() => execute(
              () => api("/api/cash/open", { method: "POST", body: JSON.stringify({ openingAmount: Number(openingAmount) }) }),
              "Caixa aberto com sucesso."
            )}>Abrir caixa</button>
          </div>
        </section>
      ) : (
        <>
          <section className={styles.sessionBar}>
            <div>
              <span className={styles.statusOpen}>CAIXA ABERTO</span>
              <strong>Desde {summary.openedAt ? new Date(summary.openedAt).toLocaleString("pt-BR") : "—"}</strong>
            </div>
            <span>{summary.movements ?? 0} movimentações</span>
          </section>

          <section className={styles.cards}>
            <Metric label="Vendas totais" value={money(summary.totalSales)} />
            <Metric label="Dinheiro esperado" value={money(summary.expectedCash)} />
            <Metric label="Pix" value={money(summary.pix)} />
            <Metric label="Cartão" value={money(summary.card)} />
            <Metric label="Dinheiro em vendas" value={money(summary.cashSales)} />
            <Metric label="Suprimentos" value={money(summary.supplies)} />
            <Metric label="Sangrias" value={money(summary.withdrawals)} />
            <Metric label="Fundo inicial" value={money(summary.openingAmount)} />
          </section>

          <section className={styles.grid}>
            <div className={styles.panel}>
              <h2>Movimentar caixa</h2>
              <div className={styles.formGrid}>
                <label>
                  Valor
                  <input type="number" min="0.01" step="0.01" value={operationAmount} onChange={e => setOperationAmount(e.target.value)} placeholder="0,00" />
                </label>
                <label>
                  Descrição
                  <input value={description} onChange={e => setDescription(e.target.value)} placeholder="Ex.: troco adicional" />
                </label>
              </div>
              <div className={styles.actions}>
                <button disabled={loading || Number(operationAmount) <= 0} onClick={() => execute(
                  () => api("/api/cash/supply", { method: "POST", body: JSON.stringify({ amount: Number(operationAmount), description }) }),
                  "Suprimento registrado."
                )}>+ Suprimento</button>
                <button className={styles.warning} disabled={loading || Number(operationAmount) <= 0} onClick={() => execute(
                  () => api("/api/cash/withdrawal", { method: "POST", body: JSON.stringify({ amount: Number(operationAmount), description }) }),
                  "Sangria registrada."
                )}>− Sangria</button>
              </div>
            </div>

            <div className={styles.panel}>
              <h2>Fechamento</h2>
              <p>Conte apenas o dinheiro físico presente na gaveta.</p>
              <label>
                Dinheiro contado
                <input type="number" min="0" step="0.01" value={countedCash} onChange={e => setCountedCash(e.target.value)} />
              </label>
              <div className={styles.closeSummary}>
                <span>Esperado <strong>{money(summary.expectedCash)}</strong></span>
                <span>Diferença <strong className={(differencePreview ?? 0) !== 0 ? styles.diff : ""}>{money(differencePreview ?? 0)}</strong></span>
              </div>
              <button className={styles.danger} disabled={loading || countedCash === ""} onClick={() => {
                if (!confirm("Confirma o fechamento do caixa?")) return;
                execute(
                  () => api("/api/cash/close", { method: "POST", body: JSON.stringify({ countedCashAmount: Number(countedCash) }) }),
                  "Caixa fechado com sucesso."
                );
              }}>Fechar caixa</button>
            </div>
          </section>

          <section className={styles.panel}>
            <h2>Movimentações do caixa atual</h2>
            <div className={styles.table}>
              <div className={styles.tableHead}><span>Tipo</span><span>Descrição</span><span>Forma</span><span>Valor</span><span>Horário</span></div>
              {movements.length ? movements.map(m => (
                <div className={styles.tableRow} key={m.id}>
                  <span className={styles.tag}>{labelType(m.type)}</span>
                  <span>{m.description}</span>
                  <span>{m.paymentMethod ?? "—"}</span>
                  <strong>{money(m.amount)}</strong>
                  <span>{new Date(m.createdAt).toLocaleString("pt-BR")}</span>
                </div>
              )) : <p>Nenhuma movimentação ainda.</p>}
            </div>
          </section>
        </>
      )}

      {message && <div className={styles.message}>{message}</div>}

      <section className={styles.panel}>
        <h2>Histórico de caixas</h2>
        <div className={styles.history}>
          {history.length ? history.map(item => (
            <div className={styles.historyRow} key={item.id}>
              <div>
                <strong>{item.status === "OPEN" ? "Aberto" : "Fechado"}</strong>
                <small>{new Date(item.openedAt).toLocaleString("pt-BR")}</small>
              </div>
              <span>Inicial: {money(item.openingAmount)}</span>
              <span>Esperado: {money(item.expectedCashAmount)}</span>
              <span>Contado: {money(item.countedCashAmount)}</span>
              <span>Diferença: {money(item.differenceAmount)}</span>
            </div>
          )) : <p>Nenhum caixa registrado.</p>}
        </div>
      </section>
    </main>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div className={styles.metric}><span>{label}</span><strong>{value}</strong></div>;
}

function labelType(type: string) {
  return ({ IN: "Venda", OPENING: "Abertura", SUPPLY: "Suprimento", WITHDRAWAL: "Sangria" } as Record<string, string>)[type] ?? type;
}
