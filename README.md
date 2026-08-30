# xadad-project-2026
An enterprise-grade, digital Quality Management System designed to transition industrial control systems and automation environments away from slow, paper-based records and spreadsheets. 

This project is being engineered over a **12-month Work Integrated Learning (WIL) placement** by a team of five Information Technology students.

## 📋 Project Overview

Traditional paper-and-spreadsheet quality regimes suffer from delayed evidence capture, manual transcription errors, limited traceability, and grueling project close-out timelines. This solution resolves these pain points by introducing a unified digital pipeline:
1. **Windows Desktop Application:** For office-based configuration, ITP authoring, calibration registries, and one-click handover dossier generation.
2. **Mobile Field Application:** For workshop and on-site inspections, supporting offline evidence capture, encrypted storage, and resilient synchronization.

---

## 🛠️ Technology Stack

Our system uses a highly maintainable, robust, single-language ecosystem to ensure ease of long-term support:
*   **Backend & Core Logic:** .NET 8 (C#)
*   **Desktop Client:** WPF / WinForms / MAUI (Blazor Hybrid) for Windows Desktop
*   **Mobile Client:** .NET MAUI (Android / iOS) for cross-platform native capability
*   **Database:** PostgreSQL (Central Hub) & SQLite (Encrypted Local Mobile Storage)
*   **Code Quality & Automation:** SonarQube, GitHub Actions, Husky.Net
*   **Testing Suite:** xUnit, Coverlet, FluentAssertions

---

## ✨ Core System Modules

### 1. Inspection & Test Plan (ITP) Management
*   Authoring engine with strict activity dependency mappings.
*   Enforcement of Hold, Witness, and Review points.
*   State-driven release workflows to control process progression.

### 2. Check Sheet Engine
*   Configurable digital templates matching industry standards (**QCS-01 to QCS-11**).
*   Strict validation on pass/fail states and numeric tolerance limits.
*   Direct mobile attachment of photo evidence and dual biometric/electronic signatures.

### 3. Mobile Field Capture
*   True offline capability allowing field engineers to execute inspections without internet.
*   Encrypted local database storage on mobile devices.
*   Resilient background synchronization with conflict-resolution logic.

### 4. Nonconformance & Punch List Management
*   Defect register automatically linked back to original inspection IDs.
*   Severity classifications with trackable, verify-to-close corrective actions.
*   Instant workflow locking of failed inspection blocks.

### 5. Calibration Register
*   Master database of physical instruments, certificates, and validation due dates.
*   Hard system enforcement preventing out-of-calibration tool assignment.

### 6. Training & Competency Records
*   Session tracking and attendance digital signature logs.
*   Competency matrix evaluations to ensure verified personnel handle inspections.

### 7. Reporting & Dashboards
*   Real-time analytical dashboards mapping project health.
*   Automated PDF and Excel data exportation.
*   One-click final handover dossier engine compiling clean, uncorrupted project histories.

### 8. Internal Audit Module
*   Comprehensive audit program scheduler and digital checklist generator.
*   System audit log captures and findings aligned to **ISO 19011** protocols.

---

## 🛡️ Non-Functional Requirements & Compliance

*   **Security:** Role-Based Access Control (RBAC). Electronic signatures are cryptographically bound to the user identity and an immutable content hash.
*   **Reliability:** Robust error handling capable of sustaining network drops during field inspection synchronizations without data loss.
*   **Compliance:** Full engineering alignment with **ISO 9001:2015** quality clauses and strict data processing guardrails to comply with **POPIA** (Protection of Personal Information Act) requirements.

---

## 👥 Version Control & Branching Strategy (Gitflow)

To manage parallel workflows among 5 developers without merge conflicts, we strictly enforce **Gitflow**:

*   `main`: Holds stable, production-ready, deployed field milestones. No direct pushes.
*   `development`: The central integration branch where code features are merged.
*   `feature/*`: Temporary branches isolated per developer module (e.g., `feature/desktop-calibration`).
*   `release/*`: Branches utilized for stability testing right before major close-outs.
*   `hotfix/*`: Fast-tracked patches branching directly off `main` to address production field crashes.

### Commit Guidelines
We enforce the **Conventional Commits** framework. Commit messages must be structured as follows:
*   `feat(mobile-ui): add electronic signature panel canvas`
*   `fix(sync): resolve postgreSQL conflict flag during mobile upload`
*   `test(desktop-itp): construct xUnit validation for locked hold points`

---

## 🤖 CI/CD Pipeline & SonarQube Quality Gates

Our repository uses **GitHub Actions** connected to **SonarQube** via the `SonarScanner for .NET`. Every time a Pull Request is opened against `development` or `main`, the system spins up an isolated Docker container with a PostgreSQL instance to build code and run `dotnet test`.

Code **cannot** be merged if it fails our Quality Gate:
*   **0 Critical Bugs** or Vulnerabilities.
*   **Less than 5% Code Smell** (Technical Debt).
*   **Minimum 80% Test Coverage** managed by Coverlet tracking.

---

## 🚀 Local Development Setup

### Prerequisites
*   [.NET 8 SDK](https://microsoft.com)
*   [PostgreSQL 16+](https://postgresql.org)
*   [Visual Studio 2022](https://microsoft.com) (with .NET MAUI and Desktop workloads installed)

### Quick Start
1. **Clone the Repository**
   ```bash
   git clone https://github.com
   cd xadad-project-2026
   ```

    **Update Local Connection Strings**
   Configure your database variables inside `appsettings.json` within the Desktop/API projects.

4. **Run Automated Test Suite**
   Ensure your local setup passes code rules before writing code:
   ```bash
   dotnet test
   ```

---

## 📅 Project Contributor Roles (IT WIL Team)
*   **Developer 1:** [Tshireletso Seqeta] — 
*   **Developer 2:** [​Kukhanya Dlanjwa ] — 
*   **Developer 3:** [Halalisile Mzobe] — 
*   **Developer 4:** [Promise Karabo Khoza] — 
*   **Developer 5:** [​Sakhile Mavimbela ] — 
