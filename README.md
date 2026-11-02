# Darya App 🌊

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?style=flat-square&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.0-3178C6?style=flat-square&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker_Compose-Supported-2496ED?style=flat-square&logo=docker&logoColor=white)](https://www.docker.com/)
[![Capacitor](https://img.shields.io/badge/Capacitor-Mobile_Android-119EFF?style=flat-square&logo=capacitor&logoColor=white)](https://capacitorjs.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

A cross-platform enterprise web and mobile application powered by an **ASP.NET Core Web API**, **Entity Framework Core (PostgreSQL)**, and a **React + TypeScript SPA (Vite)** bundled with **Capacitor** for Android deployment. Fully containerized with **Docker Compose**.

---

## 🏗️ Architecture Overview

```mermaid
graph TD
    Client["Client Layer (React / Vite / TS + Capacitor Mobile)"]
    Proxy["Docker Network / Reverse Proxy (Port 3000 -> 80)"]
    API["ASP.NET Core 8 Web API (Port 5000)"]
    Auth["Firebase Admin Auth Integration"]
    DB[("PostgreSQL 16 Database (Port 5432)")]
    
    Client --> Proxy
    Proxy --> API
    API --> Auth
    API --> DB
```

### 🧩 Key Components
- **Backend (`/server`):**
  - Built with **.NET 8 / ASP.NET Core** featuring Feature-based Clean Architecture.
  - **Entity Framework Core** with relational schema migrations against PostgreSQL.
  - Firebase Authentication integration (`FirebaseAdmin`) for secure JWT token verification.
- **Frontend & Mobile (`/client`):**
  - **React 18** Single Page Application bundled with **Vite**.
  - Strongly typed state management with **TypeScript**.
  - **Capacitor Android** bridge (`@capacitor/core`, `@capacitor/android`) for native mobile builds.
- **Infrastructure & Deployment:**
  - Multi-stage Docker builds for client and server.
  - Automated service orchestration with **Docker Compose** (`docker-compose.yml` for local dev and `docker-compose.prod.yml` for production).
  - Health check probes on PostgreSQL ensuring deterministic initialization order.

---

## 🚀 Quickstart & Running Locally

### Option 1: One-Command Docker Setup (Recommended)

Make sure [Docker](https://www.docker.com/) is installed and running, then:

```bash
# Clone the repository
git clone https://github.com/Nimasoy/darya_app.git
cd darya_app

# Start the complete stack (Postgres + Backend + Frontend)
docker compose up --build
```

- **Frontend:** Visit [http://localhost:3000](http://localhost:3000)
- **Backend API:** Accessible at [http://localhost:5000](http://localhost:5000)
- **Postgres:** Exposed on port `5432`

---

### Option 2: Running Manually (Local Development)

#### 1. Start PostgreSQL
```bash
docker run -d --name darya-postgres -p 5432:5432 -e POSTGRES_DB=darya -e POSTGRES_USER=darya_user -e POSTGRES_PASSWORD=darya_password postgres:16-alpine
```

#### 2. Run Backend
```bash
cd server
dotnet restore
dotnet run
```

#### 3. Run Frontend
```bash
cd client
npm install
npm run dev
```

---

## 📱 Mobile Build (Android via Capacitor)

```bash
cd client
npm run build
npx cap sync
npx cap open android
```

---

## 📄 License
This project is open-source under the [MIT License](LICENSE).
