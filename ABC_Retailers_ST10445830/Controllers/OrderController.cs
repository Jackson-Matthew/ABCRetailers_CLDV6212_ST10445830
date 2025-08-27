using Microsoft.AspNetCore.Mvc;
using ABC_Retailers_ST10445830.Models;
using ABC_Retailers_ST10445830.Models.ViewModels;
using ABC_Retailers_ST10445830.Services;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ABC_Retailers_ST10445830.Controllers;



namespace ABC_Retailers_ST10445830.Controllers
{
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<OrderController> _logger;
        public OrderController(IAzureStorageService storageService, ILogger<OrderController> logger)
        {
            _storageService = storageService;
            _logger = logger; 
        }
        public async Task<IActionResult> Index()
        {
            var orders = await _storageService.GetAllEntitiesAsync<Order>();
            
            foreach (var o in orders)
            {
                _logger.LogInformation("Retrieved Order: UnitPrice={UnitPrice}, TotalPrice={TotalPrice}", o.UnitPrice, o.TotalPrice);
            }
            return View(orders);
        }
        public async Task<IActionResult> Create()
        {
            var customers = await _storageService.GetAllEntitiesAsync<Customer>();
            var products = await _storageService.GetAllEntitiesAsync<Product>();

            var viewModel = new OrderCreateViewModel
            {
                Customers = customers,
                Products = products
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var customer = await _storageService.GetEntityAsync<Customer>("Customer", model.CustomerId);
                    var product = await _storageService.GetEntityAsync<Product>("Product", model.ProductId);
                    if (customer == null || product == null)
                    {
                        ModelState.AddModelError("", "Invalid customer or product selected");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    if (product.StockAvailable < model.Quantity)
                    {
                        ModelState.AddModelError("Quantity", $"Insufficient stock available: {product.StockAvailable}");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    _logger.LogInformation("Creating Order Debug Info:");
                    _logger.LogInformation("CustomerId={CustomerId}, ProductId={ProductId}", model.CustomerId, model.ProductId);
                    _logger.LogInformation("ProductName={ProductName}, ProductPrice={ProductPrice}, Quantity={Quantity}",
                        product.ProductName, product.Price, model.Quantity);

                    var order = new Order
                    {
                        CustomerId = model.CustomerId,
                        Username = customer.Username,
                        ProductId = model.ProductId,
                        ProductName = product.ProductName,
                        OrderDate = model.OrderDate,
                        Quantity = model.Quantity,
                        UnitPrice = (double)product.Price,
                        TotalPrice = (double)product.Price * model.Quantity,
                        Status = "Submitted"
                    };

                    _logger.LogInformation("Order to Save: UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
    order.UnitPrice, order.TotalPrice);

                    await _storageService.AddEntityAsync(order);

                    
                    // Update product stock
                    product.StockAvailable -= model.Quantity;
                    await _storageService.UpdateEntityAsync(product);

                    var orderMessage = new
                    {
                        OrderId = order.OrderId,
                        CustomerId = order.CustomerId,
                        CustomerName = customer.Name + " " + customer.Surname,
                        ProductName = product.ProductName,
                        Quantity = model.Quantity,
                        TotalPrice = order.TotalPrice,
                        OrderDate = order.OrderDate,
                        Status = order.Status
                    };

                    await _storageService.SendMessageAsync("order-notifications", JsonSerializer.Serialize(orderMessage));

                    var stockMessage = new
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        StockAvailable = product.StockAvailable + model.Quantity,
                        NewStock = product.StockAvailable,
                        UpdatedBy = "Order System",
                        UpdateDate = DateTime.UtcNow
                    };

                    await _storageService.SendMessageAsync("stock-updates", JsonSerializer.Serialize(stockMessage));

                    TempData["Success"] = "Order created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while creating the order: {ex.Message}");

                }
            }
            await PopulateDropdowns(model);
            return View(model);
        }
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var order = await _storageService.GetEntityAsync<Order>("Order", id);
            if (order == null)
            {
                return NotFound();
            }
            return View(order);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var order = await _storageService.GetEntityAsync<Order>("Order", id);
            if (order == null)
            {
                return NotFound();
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string rowKey, DateTime orderDate, string status)
        {
            if (string.IsNullOrEmpty(rowKey))
                return NotFound();

            try
            {
                var existingOrder = await _storageService.GetEntityAsync<Order>("Order", rowKey);
                if (existingOrder == null)
                {
                    return NotFound();
                }

                // Only update the fields that can change
                existingOrder.OrderDate = orderDate;
                existingOrder.Status = status;

                // Force overwrite (ignore concurrency issues)
                existingOrder.ETag = Azure.ETag.All;

                await _storageService.UpdateEntityAsync(existingOrder);

                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating order: {ex.Message}");
            }

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storageService.DeleteEntityAsync<Order>("Order", id);
                TempData["Success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<JsonResult> GetProcductPrice(string productId)
        {
            try
            {
                var product = await _storageService.GetEntityAsync<Product>("Product", productId);
                if (product != null)
                {
                    return Json(new
                    {
                        success = true,
                        price = product.Price,
                        stock = product.StockAvailable,
                        productName = product.ProductName
                    });
                }
                return Json(new { success = false });
            }
            catch
            {
                {
                    return Json(new { success = false });
                }
            }
        }

      public class UpdateOrderStatusRequest
{
    public string Id { get; set; }
    public string NewStatus { get; set; }
}

[HttpPost]
public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
{
    try
    {
        var order = await _storageService.GetEntityAsync<Order>("Order", request.Id);
        if (order == null)
        {
            return Json(new { success = false, message = "Order not found" });
        }

        var previousStatus = order.Status;
        order.Status = request.NewStatus;
        await _storageService.UpdateEntityAsync(order);

        // send message as before...

        return Json(new { success = true, message = $"Order status updated to {request.NewStatus}" });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _storageService.GetAllEntitiesAsync<Customer>();
            model.Products = await _storageService.GetAllEntitiesAsync<Product>();
        }
    }
}