# AI Agent Hub

# Static Landing Page & GitHub Pages Specification

**Version:** 0.1 Draft  
**Status:** Approved  
**Audience:** Frontend Developers, Product Designers, DevOps, Maintainers

---

## 1. Overview

This document specifies the architecture, design principles, component structure, and deployment workflow for the official static landing page of **AI Agent Hub** hosted on **GitHub Pages** (`https://rafaelhbrasil.github.io/AIAgentHub/`).

The landing page serves as the public marketing, discovery, and documentation gateway for the open-source repository.

---

## 2. Requirements & Goals

### 2.1 Functional Goals
- **Product Presentation:** Clearly articulate the value proposition of AI Agent Hub as a provider-agnostic, self-hosted orchestrator for CLI AI coding assistants.
- **Provider Showcase:** Highlight compatibility with official CLIs (Antigravity CLI `agy`, Claude Code `claude`, Gemini CLI `gemini`, OpenAI Codex `codex`, OpenCode `opencode`, and GitHub Copilot CLI).
- **Interactive Visual Preview:** Present realistic desktop and mobile screenshots with interactive device frame toggles.
- **Interactive Quickstart:** Provide one-click copyable CLI quickstart snippets for immediate evaluation (`dotnet run`, `git clone`, custom URLs).
- **Documentation & Open Source Links:** Seamlessly link to technical guides, architecture docs, GitHub releases, and issues.

### 2.2 Technical Goals
- **Zero Heavy Runtime Frameworks:** Written in pure semantic HTML5, modern Vanilla CSS, and lightweight modular Vanilla JavaScript.
- **Performance & SEO:** Sub-100ms load time, 100 Lighthouse performance score, zero bundle fragility, and complete OpenGraph/Twitter card metadata.
- **Automated CI/CD:** Fully automated deployment to GitHub Pages via GitHub Actions upon push to the `main` branch.
- **Fully Responsive & Dark-Themed:** Optimized for mobile phones (320px+), tablets, laptops, and ultra-wide displays (4K) with an obsidian/cyberpunk aesthetic.

---

## 3. Directory Layout

The landing page assets are located in the dedicated `site/` folder at the repository root:

```text
site/
├── index.html          # Semantic HTML5 landing page with OpenGraph & Schema metadata
├── styles.css          # Design system, CSS variables, glassmorphism, responsive grid
├── main.js             # Vanilla JS for interactive tabs, copy feedback, mobile navigation
└── assets/             # Optimized images, screenshot previews, and SVG icons
    ├── desktopview1.png
    ├── mobileview1.png
    └── favicon.svg
```

---

## 4. Design System & Styling Guidelines

### 4.1 Color Palette
- **Background Base:** Obsidian Dark (`#0B0F19`)
- **Card / Surface Fill:** Deep Navy Glass (`rgba(17, 24, 39, 0.75)` with `backdrop-filter: blur(16px)`)
- **Borders:** Translucent Slate (`rgba(255, 255, 255, 0.08)`) with dynamic hover glow (`rgba(99, 102, 241, 0.4)`)
- **Primary Brand Gradient:** Indigo to Violet (`linear-gradient(135deg, #6366F1 0%, #8B5CF6 50%, #EC4899 100%)`)
- **Accent Cyan:** `#06B6D4` (Highlights, tool badges)
- **Accent Emerald:** `#10B981` (Status badges, success copy states)
- **Text Primary:** `#F9FAFB`
- **Text Muted:** `#94A3B8`

### 4.2 Typography
- **Primary Body & Headings:** `Inter`, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif
- **Code & Snippets:** `JetBrains Mono`, 'Fira Code', Menlo, Consolas, monospace

---

## 5. Deployment Architecture (GitHub Pages)

The landing page is deployed using GitHub Actions:
- **Workflow File:** `.github/workflows/deploy-pages.yml`
- **Trigger:** Pushes to `main` modifying `site/**` or manual dispatch (`workflow_dispatch`).
- **Artifact:** Uploads the `site/` directory using `actions/upload-pages-artifact@v3`.
- **Publisher:** Deploys artifact to the GitHub Pages environment via `actions/deploy-pages@v4`.
- **Target URL:** `https://rafaelhbrasil.github.io/AIAgentHub/`
