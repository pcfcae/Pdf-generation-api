# PDF API Service

## Overview

This project provides a REST-based API for generating PDF documents from predefined templates.
It is designed to be consumed by multiple systems that require dynamic PDF generation.

---

## Features

* Template-based PDF generation
* REST API integration
* Scalable service architecture
* Centralized PDF rendering engine

---

## Technology

This service is built using:

* .NET Framework 4.7.2
* ASP.NET Web API
* iText 5 for PDF generation

---

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPLv3)**.

You may obtain a copy of the license at:
https://www.gnu.org/licenses/agpl-3.0.en.html

---

## Third-Party Components

### iText 5

This project uses **iText 5**, a PDF library licensed under the AGPLv3.

* Website: https://itextpdf.com
* License: GNU Affero General Public License v3.0

---

## Source Code Availability (AGPL Compliance)

In accordance with the AGPLv3 license requirements:

> Users interacting with this service over a network are entitled to receive the complete corresponding source code.

The full source code for this service is publicly available at:

👉 https://github.com/pcfcae/Pdf-generation-api

---

## Installation

### Prerequisites

* Windows OS
* .NET Framework 4.7.2 Developer Pack
* Visual Studio 2017 or 2019
* IIS (optional, for hosting)

---

### Steps to Run Locally

```bash
# Clone repository
git clone https://github.com/pcfcae/Pdf-generation-api

# Navigate to project folder
cd Pdf-generation-api
```

### Open in Visual Studio

1. Open Visual Studio 2017 or 2019
2. Click **Open Project/Solution**
3. Select the `.sln` file
4. Right-click solution → **Restore NuGet Packages** to install required dependencies

---

### Restore NuGet Packages

In Visual Studio:

* Right-click solution → **Restore NuGet Packages**

Or via command line:

```bash
nuget restore
```

---

### Build the Project

```bash
msbuild Pdf-generation-api.sln
```

Or use Visual Studio:

* Build → **Build Solution**

---

### Run the API

#### Option 1 — Visual Studio (Recommended)

* Press **F5** or click **Start**
* The API will run using IIS Express

#### Option 2 — IIS Deployment

1. Publish the project:

   * Right-click project → **Publish**
2. Deploy to IIS
3. Configure Application Pool:

   * .NET CLR Version: **v4.0**
4. Start the site

---

## Clients Folder

The `Clients/` folder contains a reference implementation showing how to consume this API from a **Microsoft Dynamics CRM plugin**. It is not part of the main API project.

> **Note:** If the `Clients` project is included in the solution, you must **unload or exclude** it before building. Right-click the project in Solution Explorer → **Unload Project**. Otherwise the build will fail due to missing CRM SDK dependencies.

---

## API Usage

### Generate PDF

**Endpoint:**

```
POST /api/pdf/generate
```

**Description:**
Generates a PDF document based on provided template and data.

**Request Example:**

Refer to the Testing Guide in the `DOCS` folder

---

## Compliance Notice

This service uses iText 5 under the AGPLv3 license.

If you are using this service over a network, you have the right to access the complete source code under the terms of the AGPLv3.

---

## Disclaimer

This software is provided "as-is", without warranty of any kind.

