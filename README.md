# BulkyBook – E-Commerce Web Application

## Overview

BulkyBook is an E-Commerce web application built using ASP.NET Core MVC. It provides an online bookstore where customers can browse products, add items to their shopping cart, and place orders.

The application also includes an administrative area for managing products, categories, users, and orders.

## Technologies Used

- ASP.NET Core MVC
- C#
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Bootstrap
- JavaScript & jQuery
- Chart.js
- Mailjet for email services

## Architecture

The solution follows a layered architecture to separate responsibilities and improve maintainability.

- **BulkyBook.Models:** Contains application models and ViewModels.
- **BulkyBook.DataAccess:** Handles database operations, Entity Framework Core, repositories, and Unit of Work.
- **BulkyBook.Business:** Contains business logic and application services.
- **BulkyBook.Utility:** Contains shared constants and helper classes.
- **BulkyBookWeb:** The ASP.NET Core MVC web application.

## Main Features

### Customer Features
- User registration and login.
- Browse products and categories.
- View product details.
- Add and remove products from the shopping cart.
- Update shopping cart quantities.
- Checkout and place orders.
- View order information.

### Admin Features
- Manage products (Create, Read, Update, Delete).
- Manage categories.
- Manage users and roles.
- View and manage customer orders.
- Update order status and tracking information.
- View dashboard statistics and charts.

### Additional Features
- Authentication and authorization using ASP.NET Core Identity.
- Role-based access control.
- Password reset functionality.
- Email notifications.
- Product image upload.
- Different product prices based on order quantity.

## Getting Started

### Prerequisites

- .NET SDK compatible with the project.
- SQL Server.
- Visual Studio.
- SQL Server Management Studio (SSMS).

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/MohamedAhmedHamza/BulkyBook.git
   ```

2. Open the solution in Visual Studio.

3. Configure your database connection string in your local configuration.

4. Configure the required email service settings if you want to use email functionality.

5. Apply Entity Framework Core migrations:

   ```bash
   Update-Database
   ```

6. Build and run the application.

## Security

Sensitive configuration files, database credentials, API keys, and publishing profiles are excluded from the Git repository.

Configure your own local settings before running the application.

## Author

**Mohamed Ahmed Hamza**

GitHub: [MohamedAhmedHamza](https://github.com/MohamedAhmedHamza)