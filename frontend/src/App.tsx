import { useCallback, useEffect, useState } from 'react'

// ── Types ─────────────────────────────────────────────────────────────────

interface UserInfo {
  name: string
  email: string
  isAuthenticated: boolean
}

interface Product {
  id: number
  name: string
  price: number
  category: string
}

interface Order {
  id: number
  customer: string
  total: number
  status: string
}

// ── API helpers ───────────────────────────────────────────────────────────

async function fetchJson<T>(url: string): Promise<T | null> {
  const res = await fetch(url)
  if (!res.ok) return null
  return res.json() as Promise<T>
}

async function getCsrfToken(): Promise<string> {
  const data = await fetchJson<{ requestToken: string }>('/bff/csrf')
  return data?.requestToken ?? ''
}

// ── App ───────────────────────────────────────────────────────────────────

export default function App() {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [products, setProducts] = useState<Product[] | null>(null)
  const [orders, setOrders] = useState<Order[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [loginName, setLoginName] = useState('demo-user')

  const refreshUser = useCallback(async () => {
    const u = await fetchJson<UserInfo>('/bff/user')
    setUser(u)
  }, [])

  useEffect(() => {
    refreshUser().finally(() => setLoading(false))
  }, [refreshUser])

  const handleLogin = () => {
    window.location.href = `/bff/login?username=${encodeURIComponent(loginName)}`
  }

  const handleLogout = async () => {
    const token = await getCsrfToken()
    await fetch('/bff/logout', {
      method: 'POST',
      headers: { 'X-CSRF-TOKEN': token },
    })
    setUser(null)
    setProducts(null)
    setOrders(null)
  }

  const loadData = async () => {
    const [p, o] = await Promise.all([
      fetchJson<Product[]>('/api/catalog/products'),
      fetchJson<Order[]>('/api/orders/orders'),
    ])
    setProducts(p)
    setOrders(o)
  }

  if (loading) return <p style={styles.center}>Loading…</p>

  return (
    <div style={styles.container}>
      <h1 style={styles.heading}>🚀 Aspire BFF Demo</h1>

      {/* Auth section */}
      <section style={styles.card}>
        <h2>Authentication</h2>
        {user ? (
          <>
            <p>
              ✅ Signed in as <strong>{user.name}</strong> ({user.email})
            </p>
            <button style={styles.btn} onClick={handleLogout}>
              Logout
            </button>
          </>
        ) : (
          <>
            <p>❌ Not signed in</p>
            <input
              style={styles.input}
              value={loginName}
              onChange={e => setLoginName(e.target.value)}
              placeholder="username"
            />
            <button style={styles.btn} onClick={handleLogin}>
              Login (fake dev auth)
            </button>
          </>
        )}
      </section>

      {/* Data section */}
      {user && (
        <>
          <section style={styles.card}>
            <h2>API Data</h2>
            <button style={styles.btn} onClick={loadData}>
              Load Products &amp; Orders
            </button>
          </section>

          {products && (
            <section style={styles.card}>
              <h2>📦 Catalog — /api/catalog/products</h2>
              <table style={styles.table}>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Price</th>
                    <th>Category</th>
                  </tr>
                </thead>
                <tbody>
                  {products.map(p => (
                    <tr key={p.id}>
                      <td>{p.id}</td>
                      <td>{p.name}</td>
                      <td>${p.price.toFixed(2)}</td>
                      <td>{p.category}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          {orders && (
            <section style={styles.card}>
              <h2>🛒 Orders — /api/orders/orders</h2>
              <table style={styles.table}>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Customer</th>
                    <th>Total</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {orders.map(o => (
                    <tr key={o.id}>
                      <td>{o.id}</td>
                      <td>{o.customer}</td>
                      <td>${o.total.toFixed(2)}</td>
                      <td>{o.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}
        </>
      )}

      {/* Links */}
      <section style={styles.card}>
        <h2>Links</h2>
        <ul>
          <li>
            <a href="/health" target="_blank" rel="noreferrer">
              /health — Gateway liveness
            </a>
          </li>
          <li>
            <a href="/bff/user" target="_blank" rel="noreferrer">
              /bff/user — Current user info (JSON)
            </a>
          </li>
          <li>
            <a href="/bff/csrf" target="_blank" rel="noreferrer">
              /bff/csrf — CSRF token (JSON)
            </a>
          </li>
        </ul>
      </section>
    </div>
  )
}

// ── Minimal inline styles ─────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
  container: { maxWidth: 800, margin: '0 auto', padding: '2rem', fontFamily: 'system-ui, sans-serif' },
  heading: { borderBottom: '2px solid #ddd', paddingBottom: '0.5rem' },
  card: { background: '#f9f9f9', border: '1px solid #ddd', borderRadius: 8, padding: '1rem', marginBottom: '1rem' },
  btn: { padding: '0.4rem 1rem', cursor: 'pointer', marginLeft: '0.5rem', borderRadius: 4 },
  input: { padding: '0.4rem', borderRadius: 4, border: '1px solid #ccc', width: 160 },
  center: { textAlign: 'center', marginTop: '4rem' },
  table: { width: '100%', borderCollapse: 'collapse' as const },
}
