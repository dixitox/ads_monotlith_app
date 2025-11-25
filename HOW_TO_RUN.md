# 🚀 How to Run and Access the Applications

## Quick Access Guide

### Running Locally (Current Best Option)

Open **TWO separate PowerShell terminals** and run:

**Terminal 1 - RetailMonolith:**
```powershell
cd c:\Users\edinmedi\source\repos\ads_monotlith_app
dotnet run --project .\RetailMonolith.csproj
```
Access at: **http://localhost:5068**

**Terminal 2 - RetailDecomposed:**
```powershell
cd c:\Users\edinmedi\source\repos\ads_monotlith_app
dotnet run --project .\RetailDecomposed\RetailDecomposed.csproj
```
Access at: **http://localhost:6068**

---

## Using the Script (Automated)

### Run Locally:
```powershell
.\run-both-apps.ps1 -Mode local
```
- RetailMonolith: http://localhost:5068
- RetailDecomposed: http://localhost:6068

### Run in Containers:
```powershell
.\run-both-apps.ps1 -Mode container
```
- RetailMonolith: http://localhost:5068
- RetailDecomposed: http://localhost:8080
- Products API: http://localhost:8081
- Cart API: http://localhost:8082
- Orders API: http://localhost:8083
- Checkout API: http://localhost:8084

---

## What You'll See

### RetailMonolith (http://localhost:5068)
- Home page with product listings
- Product browsing
- Shopping cart functionality
- Order history
- Traditional monolithic architecture

### RetailDecomposed (http://localhost:6068 or 8080)
- Same features as monolith
- AI-powered product search (if Azure AI configured)
- Semantic search
- Decomposed architecture with independent services
- Modern authentication (Azure AD)

---

## Testing the Apps

Once running, try these actions:

1. **Browse Products**: Click "Products" in navigation
2. **Add to Cart**: Click "Add to Cart" on any product
3. **View Cart**: Click "Cart" to see items
4. **Place Order**: Click "Checkout" to create an order
5. **View Orders**: Click "Orders" to see order history

---

## Stopping the Applications

### If running locally (separate terminals):
- Press **Ctrl+C** in each terminal

### If using the script:
- Press **Ctrl+C** in the terminal running the script
- It will automatically stop both apps (local) or all containers (container mode)

---

## Troubleshooting

### Port Already in Use
If you see "Address already in use" error:
```powershell
# Find what's using the port (e.g., 5068)
netstat -ano | findstr :5068

# Kill the process (replace PID with the number from above)
taskkill /PID <PID> /F
```

### Database Not Found
Both apps will automatically create their databases on first run using Entity Framework migrations. Just wait a few seconds after starting.

### Can't Access the Application
1. Make sure the terminal shows "Now listening on: http://localhost:5068"
2. Check Windows Firewall isn't blocking the ports
3. Try accessing http://127.0.0.1:5068 instead

---

## Next Steps

After starting the applications:

1. ✅ Access RetailMonolith at http://localhost:5068
2. ✅ Access RetailDecomposed at http://localhost:6068
3. ✅ Run tests: `.\Tests\run-all-tests.ps1`
4. ✅ Try container mode: `.\run-both-apps.ps1 -Mode container`

For more details, see **QUICK_START.md**
