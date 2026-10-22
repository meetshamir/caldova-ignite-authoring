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
      <article class="card"><h2>Reports</h2><p class="meta">Daily sales reporting. Visit <a href="#/reports">Reports</a>.</p></article>
      <article class="card"><h2>Recommendations</h2><p class="meta">Personalized recommendations. Visit <a href="#/recommendations">Recommendations</a>.</p></article>
    </section>`;
  try {
    const status = await getJson("/api/status");
    document.getElementById("status").innerHTML = [
      [`${status.uptimeSeconds}s`, "Uptime"],
      [status.dotnetVersion, ".NET runtime"],
      [status.instance, "Instance"]
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

function renderSearch() {
  app.innerHTML = `<section class="panel">
    <h2>Search</h2>
    <p class="meta">Browse the catalog page by page.</p>
    <form id="search-form"><input id="search-page" type="text" placeholder="Page (e.g. 1)" value="1"> <button type="submit">Search</button></form>
    <div id="search-results"></div>
  </section>`;
  document.getElementById("search-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const page = document.getElementById("search-page").value;
    const results = document.getElementById("search-results");
    results.innerHTML = `<p class="meta">Searching&hellip;</p>`;
    try {
      const result = await getJson(`/api/search?page=${encodeURIComponent(page)}`);
      results.innerHTML = `<p class="meta">Page ${escapeHtml(result.page)} of ${escapeHtml(result.totalCount)} items &middot; catalog: ${escapeHtml(result.catalogWave)}</p>
        <div class="grid">${result.products.map((product) =>
          `<article class="card"><h2>${escapeHtml(product.name)}</h2><p class="meta">SKU ${escapeHtml(product.sku)} &middot; $${escapeHtml(product.price.toFixed(2))}</p></article>`
        ).join("")}</div>`;
    } catch (error) {
      results.innerHTML = errorPanel(error.status || 500, "Search failed", error.message);
    }
  });
}

function renderReports() {
  app.innerHTML = `<section class="panel">
    <h2>Daily sales report</h2>
    <p class="meta">1 = Monday &hellip; 7 = Sunday.</p>
    <form id="reports-form"><input id="reports-day" type="text" placeholder="Day (1-7)" value="1"> <button type="submit">View</button></form>
    <div id="reports-results"></div>
  </section>`;
  document.getElementById("reports-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const day = document.getElementById("reports-day").value;
    const results = document.getElementById("reports-results");
    results.innerHTML = `<p class="meta">Loading&hellip;</p>`;
    try {
      const report = await getJson(`/api/reports?day=${encodeURIComponent(day)}`);
      results.innerHTML = `<p>Day ${escapeHtml(report.day)}: $${escapeHtml(report.sales)} in sales.</p>`;
    } catch (error) {
      results.innerHTML = errorPanel(error.status || 500, "Report unavailable", error.message);
    }
  });
}

function renderRecommendations() {
  app.innerHTML = `<section class="panel">
    <h2>Recommended for you</h2>
    <p class="meta">Enter a customer ID to see their personalized recommendations.</p>
    <form id="recommendations-form"><input id="recommendations-customer" type="text" placeholder="Customer ID (e.g. RETURNING-0001)" value="RETURNING-0001"> <button type="submit">Load</button></form>
    <div id="recommendations-results"></div>
  </section>`;
  document.getElementById("recommendations-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const customerId = document.getElementById("recommendations-customer").value;
    const results = document.getElementById("recommendations-results");
    results.innerHTML = `<p class="meta">Loading&hellip;</p>`;
    try {
      const result = await getJson(`/api/recommendations?customerId=${encodeURIComponent(customerId)}`);
      results.innerHTML = `<p class="meta">Segment: ${escapeHtml(result.segment)}</p>
        <div class="grid">${result.items.map((item) =>
          `<article class="card"><h2>${escapeHtml(item.name)}</h2><p class="meta">SKU ${escapeHtml(item.sku)} &middot; $${escapeHtml(item.price.toFixed(2))}</p></article>`
        ).join("")}</div>`;
    } catch (error) {
      results.innerHTML = errorPanel(error.status || 500, "Recommendations unavailable", error.message);
    }
  });
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
