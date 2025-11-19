# Retail Monolith Architecture

```mermaid
graph TB
    Browser[Web Browser]
    
    HomePage[Home Page]
    ProductsPage[Products Page]
    CartPage[Cart Page]
    CheckoutPage[Checkout Page]
    OrdersPage[Orders Page]
    OrderDetails[Order Details]
    
    CartService[Cart Service]
    CheckoutService[Checkout Service]
    PaymentGateway[Payment Gateway]
    
    DbContext[EF Core DbContext]
    
    Products[(Products DB)]
    Inventory[(Inventory DB)]
    Carts[(Carts DB)]
    Orders[(Orders DB)]
    SqlServer[(SQL Server)]
    
    Browser --> HomePage
    Browser --> ProductsPage
    Browser --> CartPage
    Browser --> CheckoutPage
    Browser --> OrdersPage
    Browser --> OrderDetails

    ProductsPage --> DbContext
    CartPage --> CartService
    CheckoutPage --> CheckoutService
    OrdersPage --> DbContext
    OrderDetails --> DbContext

    CartService --> DbContext
    CheckoutService --> DbContext
    CheckoutService --> PaymentGateway
    CheckoutService --> CartService

    DbContext --> Products
    DbContext --> Inventory
    DbContext --> Carts
    DbContext --> Orders

    Products --> SqlServer
    Inventory --> SqlServer
    Carts --> SqlServer
    Orders --> SqlServer

    style Browser fill:#e1f5ff
    style CartService fill:#fff3cd
    style CheckoutService fill:#fff3cd
    style PaymentGateway fill:#fff3cd
    style DbContext fill:#d4edda
    style SqlServer fill:#d4edda
```

## Component Descriptions

### **Presentation Layer (Razor Pages)**
- **Pages**: Server-rendered HTML views with C# code-behind
- Handles user interactions and displays data
- Routes: `/`, `/Products`, `/Cart`, `/Checkout`, `/Orders`

### **Application Layer (Services)**
- **CartService**: Add/remove items, update quantities, clear cart
- **CheckoutService**: Orchestrates checkout workflow
  1. Retrieves cart
  2. Reserves inventory
  3. Processes payment
  4. Creates order
  5. Clears cart
- **MockPaymentGateway**: Simulates payment processing (always succeeds)

### **Data Layer**
- **AppDbContext**: EF Core context managing all database operations
- **Models**: Product, Cart, CartLine, Order, OrderLine, InventoryItem
- **Migrations**: Database schema versioning

### **Infrastructure**
- **SQL Server LocalDB**: Development database (swap to Azure SQL for production)
- **Health Checks**: `/health` endpoint for monitoring
- **Minimal APIs**: REST endpoints for headless checkout scenarios

## Data Flow: Complete Checkout Example

```mermaid
sequenceDiagram
    participant U as User/Browser
    participant CP as Checkout Page
    participant CS as CheckoutService
    participant PG as PaymentGateway
    participant DB as AppDbContext
    participant SQL as SQL Server

    U->>CP: Click "Place Order"
    CP->>CS: CheckoutAsync(customerId, paymentToken)
    
    CS->>DB: Get Cart with Lines
    DB->>SQL: SELECT * FROM Carts WHERE CustomerId = ?
    SQL-->>DB: Cart Data
    DB-->>CS: Cart Object
    
    CS->>CS: Calculate Total
    
    loop For each cart line
        CS->>DB: Get Inventory by SKU
        DB->>SQL: SELECT * FROM Inventory WHERE Sku = ?
        SQL-->>DB: Inventory Data
        DB-->>CS: InventoryItem
        CS->>CS: Check Stock Available
        CS->>DB: Decrement Inventory.Quantity
    end
    
    CS->>PG: ChargeAsync(amount, currency, token)
    PG-->>CS: PaymentResult (Success)
    
    CS->>CS: Create Order Object
    CS->>DB: Add Order with OrderLines
    CS->>DB: Remove Cart and CartLines
    CS->>DB: SaveChangesAsync()
    DB->>SQL: BEGIN TRANSACTION
    DB->>SQL: INSERT Orders
    DB->>SQL: DELETE Cart
    DB->>SQL: COMMIT
    SQL-->>DB: Success
    
    DB-->>CS: Order Created
    CS-->>CP: Order Object
    CP-->>U: Redirect to Orders/Details
```

## Key Monolith Characteristics

1. **Single Deployment Unit**: One application, one codebase
2. **Shared Database**: All features access the same SQL Server database
3. **In-Process Communication**: Services call each other via dependency injection
4. **Transactional Consistency**: Database transactions span multiple tables
5. **Tight Coupling**: Checkout depends on Cart, Payment, Inventory, and Orders

## Technology Stack

- **Framework**: ASP.NET Core 9.0
- **UI**: Razor Pages (server-side rendering)
- **ORM**: Entity Framework Core 9.0
- **Database**: SQL Server / LocalDB
- **DI Container**: Built-in ASP.NET Core DI
- **Frontend**: Bootstrap 5, jQuery (from CDN)

---

*This diagram represents the current monolithic architecture. The app is designed to demonstrate migration patterns toward microservices decomposition.*
