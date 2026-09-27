# 02 · UML

## 2.1 Domain model (class diagram)

```mermaid
classDiagram
    direction LR

    class Franchise {
        +Guid Id
        +string Code
        +string Name
        +string City
        +string PrimaryColor
    }
    class ProductCategory {
        +Guid Id
        +string Code
        +string Name
    }
    class Product {
        +Guid Id
        +string Sku
        +string Name
        +decimal Price
        +string Currency
        +int StockQuantity
        +bool IsActive
        +uint Version  «xmin»
        +CanFulfil(qty) bool
        +Deactivate()
    }
    class Customer {
        +Guid Id
        +string Email
        +string FullName
    }
    class Cart {
        <<aggregate root>>
        +Guid Id
        +Guid CustomerId
        +DateTimeOffset UpdatedAt
        +IReadOnlyCollection~CartItem~ Items
        +bool IsEmpty
        +AddItem(productId, qty, CartPolicy, now) CartItem
        +SetItemQuantity(productId, qty, CartPolicy, now)
        +RemoveItem(productId, now)
        +Clear(now)
    }
    class CartItem {
        +Guid Id
        +Guid CartId
        +Guid CustomerId  «shard key»
        +Guid ProductId
        +int Quantity
    }
    class CartPolicy {
        <<value object>>
        +int MaxQuantityPerLine
        +int MaxDistinctLines
    }
    class Order {
        <<aggregate root>>
        +Guid Id
        +string OrderNumber
        +Guid CustomerId
        +string IdempotencyKey
        +OrderStatus Status
        +decimal Subtotal / Tax / Shipping / Total
        +int ItemCount  «denormalised»
        +string CustomerName / CustomerEmail «snapshot»
        +Place(OrderPlacement)$ Order
    }
    class OrderItem {
        +Guid ProductId
        +string Sku / ProductName / FranchiseName / CategoryName «snapshot»
        +decimal UnitPrice
        +int Quantity
        +decimal LineTotal
    }
    class OrderPlacement {
        <<parameter object>>
    }
    class OrderLine {
        <<value object>>
        +decimal LineTotal
    }
    class PriceBreakdown {
        <<value object>>
        +decimal Subtotal
        +decimal Tax
        +decimal Shipping
        +decimal Total
    }
    class OrderStatus {
        <<enumeration>>
        Placed
        Paid
        Shipped
        Delivered
        Cancelled
    }

    Product "*" --> "1" Franchise
    Product "*" --> "1" ProductCategory
    Customer "1" -- "0..1" Cart
    Cart "1" *-- "*" CartItem
    CartItem "*" ..> "1" Product : ProductId
    Cart ..> CartPolicy : uses
    Customer "1" -- "*" Order
    Order "1" *-- "1..*" OrderItem
    Order ..> OrderPlacement : built from
    OrderPlacement o-- OrderLine
    OrderPlacement o-- PriceBreakdown
    Order --> OrderStatus
```

## 2.2 Application ports and Infrastructure adapters

```mermaid
classDiagram
    direction TB
    class ICheckoutService {
        <<interface>>
        +PlaceOrderAsync(PlaceOrderCommand) CheckoutResult
    }
    class ICartService {
        <<interface>>
        +GetAsync()
        +AddItemAsync()
        +UpdateItemAsync()
        +RemoveItemAsync()
    }
    class ICatalogService {
        <<interface>>
        +SearchAsync(SearchProductsQuery)
        +GetProductAsync(id)
    }
    class IOrderService {
        <<interface>>
        +ListAsync()
        +GetAsync()
    }

    class IUnitOfWork {

        <<interface>>

        +ExecuteInTransactionAsync(work)

    }
    class ICartRepository {
        <<interface>>
        +GetOrCreateForUpdateAsync(customerId) Cart
    }
    class IProductRepository {
        <<interface>>
        +FindAsync()
        +GetWithReferencesAsync()
        +TryReserveStockAsync() bool
    }
    class IOrderRepository {
        <<interface>>
        +FindByIdempotencyKeyAsync()
        +Add(Order)
    }
    class ICatalogQueries {
        <<interface>>
        +SearchAsync(criteria, page)
        +GetDetailsAsync()
    }
    class IOrderQueries {
        <<interface>>
        +ListAsync()
        +GetAsync()
    }
    class ICartQueries {
        <<interface>>
        +GetAsync(customerId) CartView
    }
    class IPricingPolicy {
        <<interface>>
        +Calculate(PricingRequest) PriceBreakdown
    }
    class IOrderNumberGenerator {
        <<interface>>
        +Next(placedAt) string
    }
    class ICatalogFilter {
        <<interface>>
        +Apply(query, criteria)
    }

    CheckoutService ..|> ICheckoutService
    CartService ..|> ICartService
    CatalogService ..|> ICatalogService
    OrderService ..|> IOrderService
    StandardPricingPolicy ..|> IPricingPolicy

    CheckoutService --> IUnitOfWork
    CheckoutService --> ICartRepository
    CheckoutService --> IProductRepository
    CheckoutService --> IOrderRepository
    CheckoutService --> IPricingPolicy
    CheckoutService --> IOrderNumberGenerator
    CartService --> ICartQueries
    CatalogService --> ICatalogQueries

    EfUnitOfWork ..|> IUnitOfWork
    CartRepository ..|> ICartRepository
    ProductRepository ..|> IProductRepository
    OrderRepository ..|> IOrderRepository
    CatalogQueries ..|> ICatalogQueries
    CatalogQueries --> "*" ICatalogFilter
    OrderQueries ..|> IOrderQueries
    CartQueries ..|> ICartQueries
    OrderNumberGenerator ..|> IOrderNumberGenerator
    SearchTermFilter ..|> ICatalogFilter
    FranchiseFilter ..|> ICatalogFilter
    CategoryFilter ..|> ICatalogFilter
    PriceRangeFilter ..|> ICatalogFilter
    InStockFilter ..|> ICatalogFilter
```

## 2.3 Sequence: concurrent "add to cart" for the same customer

```mermaid
sequenceDiagram
    participant A as Request A
    participant B as Request B
    participant DB as PostgreSQL
    A->>DB: BEGIN · INSERT carts ON CONFLICT DO NOTHING (inserts)
    B->>DB: BEGIN · INSERT carts ON CONFLICT DO NOTHING
    Note over B,DB: waits: conflicting uncommitted row
    A->>DB: SELECT ... FOR UPDATE (own row) · INSERT cart_item qty=1 · COMMIT
    DB-->>B: conflict resolved -> DO NOTHING
    B->>DB: SELECT ... FOR UPDATE -> sees A's committed line
    B->>DB: UPDATE cart_items SET quantity=2 · COMMIT
    Note over A,B: result: 1 cart, 1 line, quantity 2 (no duplicate, no lost update)
```

## 2.4 State machine: order status (roadmap for payments and fulfilment)

```mermaid
stateDiagram-v2
    [*] --> Placed : checkout
    Placed --> Paid : payment captured
    Placed --> Cancelled : payment failed / customer cancels
    Paid --> Shipped
    Shipped --> Delivered
    Paid --> Cancelled : refund
    Delivered --> [*]
    Cancelled --> [*]
```
