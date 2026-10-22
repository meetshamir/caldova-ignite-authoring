"use strict";

const app = document.getElementById("app");
const navLinks = document.querySelectorAll("#nav a");

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (char) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[char]));
}

async function getJson(path) {
  const response = await fetch(path);
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(body.detail || body.error || `Request failed with HTTP ${response.status}`);
    error.status = response.status;
    error.body = body;
    throw error;
  }
  return body;
}

function setActiveNav(route) {
  navLinks.forEach((link) => {
    link.classList.toggle("active", link.dataset.route === route);
  });
}

function errorPanel(status, title, message) {
  return `<section class="panel"><h2 class="error">${status} - ${escapeHtml(title)}</h2><p>${escapeHtml(message)}</p></section>`;
}

async function renderDashboard() {
  app.innerHTML = `<section class="status" id="status">Loading Caldova status&hellip;</section>
    <section class="grid">
      <article class="card"><h2>Checkout</h2><p class="meta">Order checkout flow. Visit <a href="#/checkout">Checkout</a>.</p></article>
      <article class="card"><h2>Search</h2><p class="meta">Product search. Visit <a href="#/search">Search</a>.</p></article>
      <article class="card"><h2>Reports</h2><p class="meta">Sales reporting. Visit <a href="#/reports">Reports</a>.</p></article>
      <article class="card"><h2>Recommendations</h2><p class="meta">Personalized recommendations. Visit <a href="#/recommendations">Recommendations</a>.</p></article>
    </section>`;
  try {
    const status = await getJson("/api/status");
    document.getElementById("status").innerHTML = [
      [`${status.uptimeSeconds}s`, "Uptime"],
      [status.dotnetVersion, ".NET runtime"],
      [status.instance, "Instance"],
      [status.reportsCached, "Reports cached"]
    ].map(([value, label]) => `<div class="metric"><strong>${escapeHtml(value)}</strong><span>${label}</span></div>`).join("");
  } catch (error) {
    document.getElementById("status").innerHTML = `<span class="error">Caldova status is unavailable: ${escapeHtml(error.message)}</span>`;
  }
}

async function renderCheckout() {
  app.innerHTML = `<section class="panel">Loading checkout&hellip;</section>`;
  try {
    const order = await getJson("/api/checkout");
    app.innerHTML = `<section class="panel">
      <h2>Checkout</h2>
      <p>Order #${escapeHtml(order.orderId)} &mdash; ${escapeHtml(order.items)} items &mdash; $${escapeHtml(order.total.toFixed(2))}</p>
      <p class="meta">Payment authorized. Confirmation sent.</p>
    </section>`;
  } catch (error) {
    app.innerHTML = errorPanel(error.status || 500, "Checkout is unavailable", error.message);
  }
}

async function renderSearch() {
  app.innerHTML = `<section class="panel">Searching&hellip;</section>`;
  try {
    const result = await getJson("/api/search");
    app.innerHTML = `<section class="panel">
      <h2>Search results</h2>
      <p class="meta">${result.products.length} products found${result.delayMs > 0 ? ` (artificial delay: ${result.delayMs}ms)` : ""}.</p>
      <div class="grid">${result.products.map((product) =>
        `<article class="card"><h2>${escapeHtml(product.name)}</h2><p class="meta">SKU ${escapeHtml(product.sku)} &middot; $${escapeHtml(product.price.toFixed(2))}</p></article>`
      ).join("")}</div>
    </section>`;
  } catch (error) {
    app.innerHTML = errorPanel(error.status || 500, "Search is unavailable", error.message);
  }
}

async function renderReports() {
  app.innerHTML = `<section class="panel">Loading report&hellip;</section>`;
  try {
    const report = await getJson("/api/reports");
    app.innerHTML = `<section class="panel">
      <h2>Sales report</h2>
      <p>Last ${escapeHtml(report.periodDays)} days: ${escapeHtml(report.orders)} orders, $${escapeHtml(report.revenue.toFixed(2))} revenue.</p>
      <p class="meta">Cached report buffers held in memory: <code>${escapeHtml(report.reportsCached)}</code></p>
    </section>`;
  } catch (error) {
    app.innerHTML = errorPanel(error.status || 500, "Reports unavailable", error.message);
  }
}

async function renderRecommendations() {
  app.innerHTML = `<section class="panel">Loading recommendations&hellip;</section>`;
  try {
    const result = await getJson("/api/recommendations");
    app.innerHTML = `<section class="panel">
      <h2>Recommended for you</h2>
      <p class="meta">Based on recent purchases.</p>
      <div class="grid">${result.items.map((item) =>
        `<article class="card"><h2>${escapeHtml(item.name)}</h2><p class="meta">SKU ${escapeHtml(item.sku)} &middot; $${escapeHtml(item.price.toFixed(2))}</p></article>`
      ).join("")}</div>
    </section>`;
  } catch (error) {
    app.innerHTML = errorPanel(error.status || 404, "Recommendations not found", error.message);
  }
}

const routes = {
  "/": renderDashboard,
  "/checkout": renderCheckout,
  "/search": renderSearch,
  "/reports": renderReports,
  "/recommendations": renderRecommendations
};

function renderRoute() {
  const route = (location.hash.replace(/^#/, "") || "/");
  setActiveNav(route);
  (routes[route] || renderDashboard)();
}

window.addEventListener("hashchange", renderRoute);
window.addEventListener("DOMContentLoaded", renderRoute);
