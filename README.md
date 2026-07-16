# Vantio — Backend API (.NET Core)

**Vantio** is an enterprise-grade, AI-powered travel-planning platform. This repository contains the backend RESTful API built with **ASP.NET Core**, serving as the core engine for user authentication, AI orchestration, third-party integrations, and database management.

For the Angular frontend repository, please refer to the client-side repository.

---

## 🌟 Overview & Architecture

The backend is engineered with a strict adherence to **Clean Code** principles and the **3-Tier Architecture**, ensuring high cohesion and low coupling across the application:

1.  **Presentation Layer (PL):** ASP.NET Core Web API controllers handling HTTP requests, responses, and routing.
2.  **Business Logic Layer (BLL):** Contains core business rules, AI orchestration logic via Semantic Kernel, and third-party API consumption services.
3.  **Data Access Layer (DAL):** Manages database interactions using Entity Framework Core.

### 🏗️ Design Patterns Implemented
*   **Repository Design Pattern:** Abstracts data access logic, making the code more testable and maintainable.
*   **Unit of Work:** Manages database transactions efficiently to ensure data integrity across multiple repository operations.
*   **Caching Strategy:** Utilized custom dictionary caching to optimize database context lifecycles and significantly reduce redundant query execution times.

---

## 🛠️ Tech Stack

| Concern | Technology |
| --- | --- |
| Framework | **.NET 10 / ASP.NET Core Web API** |
| Database | **MS SQL Server** |
| ORM | **Entity Framework (EF) Core** |
| AI Orchestration | **Microsoft Semantic Kernel** & OpenAI API (GPT-4o-mini) |
| Authentication | **ASP.NET Core Identity**, **JWT** (Access & Refresh Tokens), Google OAuth 2.0 |
| Payment Gateway | **Paymob** (with secure Webhook handlers) |
| Architecture | 3-Tier Architecture, SOLID Principles, LINQ |

---

## 🔌 Third-Party API Integrations

The Vantio backend acts as an orchestrator, aggregating data from multiple external services to generate personalized itineraries:
*   **Flight Data:** AirLabs / Aero APIs
*   **Hotel Listings:** StayAPI
*   **Local Attractions & POIs:** Foursquare API
*   **Weather Forecasts:** Visual Crossing Weather API
*   **Payment Processing:** Paymob Gateway

---

## 🚀 Getting Started

### Prerequisites
*   [.NET SDK](https://dotnet.microsoft.com/download) (Version corresponding to the project)
*   [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB or Developer Edition)
*   IDE: Visual Studio 2022 or JetBrains Rider

### Setup Instructions

**1. Clone the repository**
git clone [https://github.com/YourUsername/Backend-Smart-Travel-Planers.git](https://github.com/YourUsername/Backend-Smart-Travel-Planers.git) 
cd Backend-Smart-Travel-Planers

2. Configure Environment Variables
Open appsettings.json (or create an appsettings.Development.json file) and configure your database connection string and required API keys. 
Never commit your actual API keys to version control.

JSON
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=VantioDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "Your_Super_Secret_Key_Here",
    "Issuer": "VantioAPI",
    "Audience": "VantioUsers"
  },
  "ApiKeys": {
    "OpenAI": "Your_OpenAI_Key",
    "Paymob": "Your_Paymob_Keys",
    "Foursquare": "Your_Foursquare_Key"
  }
}

3. Apply Database Migrations
The project uses EF Core Code-First approach. Run the following command to create the database schema and apply seed data:
Bash
dotnet ef database update

5. Run the API
Bash
dotnet run
The API will be available at https://localhost:5001 (or the port specified in your launchSettings.json). You can explore the endpoints using the built-in Swagger UI.
```bash
git clone [https://github.com/YourUsername/Backend-Smart-Travel-Planers.git](https://github.com/YourUsername/Backend-Smart-Travel-Planers.git)
cd Backend-Smart-Travel-Planers
