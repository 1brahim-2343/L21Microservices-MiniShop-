using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.API.Data;
using OrderService.API.Models;
using OrderService.API.Services;
using System.Net;
using System.Net.Mail;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly IProductServiceClient _productService;
    private readonly INotificationServiceClient _notificationService;

    public OrdersController(
        OrderDbContext context,
        IProductServiceClient productService,
        INotificationServiceClient notificationService)
    {
        _context = context;
        _productService = productService;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _context.Orders
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _context.Orders
            .FindAsync(id);

        if (order is null)
            return NotFound("Order not found.");

        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest(
                "Quantity must be greater than zero.");

        // 1. ProductService-dən məhsulu alırıq
        var product = await _productService
            .GetProductAsync(request.ProductId);


        if (product is null)
            return BadRequest("Product not found.");

        // 2. Stock yoxlayırıq
        if (product.Stock < request.Quantity)
            return BadRequest("Not enough stock.");

        // 3. Order yaradırıq
        var order = new Order
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = request.Quantity,
            UnitPrice = product.Price,
            TotalPrice = product.Price * request.Quantity,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        // 4. ProductService-ə stock azalt deyirik
        var stockReduced =
            await _productService.ReduceStockAsync(
                product.Id,
                request.Quantity);

        if (!stockReduced)
        {
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();

            return BadRequest(
                "Order could not be completed because stock update failed.");
        }


        //Confirmation mail:
        string fromEmail = "moresam193@gmail.com";
        string appPassword = "bqie vown jpna harc\r\n";


        string toEmail = request.Email;

        var mail = new MailMessage();

        mail.From = new MailAddress(fromEmail);
        mail.To.Add(toEmail);

        mail.IsBodyHtml = true;
        mail.Subject = $"Your MiniShop order #{order.Id} is confirmed";
        mail.Body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#222;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f5f7;padding:24px 0;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='max-width:600px;width:100%;background:#ffffff;border-radius:8px;'>

        <tr>
          <td style='background-color:#2d6cdf;padding:24px 32px;color:#ffffff;'>
            <h1 style='margin:0;font-size:22px;'>MiniShop</h1>
          </td>
        </tr>

        <tr>
          <td style='padding:32px 32px 8px 32px;'>
            <h2 style='margin:0 0 12px 0;font-size:20px;'>Thanks for your order!</h2>
            <p style='margin:0;font-size:15px;line-height:1.5;color:#555;'>
              Hi {WebUtility.HtmlEncode(request.Email)},<br>
              We've received your order and it's being processed.
            </p>
          </td>
        </tr>

        <tr>
          <td style='padding:16px 32px 0 32px;font-size:14px;color:#555;'>
            <strong>Order #:</strong> {order.Id}<br>
            <strong>Date:</strong> {order.CreatedAt:dd MMM yyyy, HH:mm} UTC
          </td>
        </tr>

        <tr>
          <td style='padding:20px 32px;'>
            <table width='100%' cellpadding='0' cellspacing='0' style='font-size:14px;border-collapse:collapse;'>
              <tr style='background-color:#f4f5f7;'>
                <th align='left'   style='padding:10px;border-bottom:1px solid #e1e4e8;'>Product</th>
                <th align='center' style='padding:10px;border-bottom:1px solid #e1e4e8;'>Qty</th>
                <th align='right'  style='padding:10px;border-bottom:1px solid #e1e4e8;'>Unit price</th>
                <th align='right'  style='padding:10px;border-bottom:1px solid #e1e4e8;'>Total</th>
              </tr>
              <tr>
                <td style='padding:12px 10px;border-bottom:1px solid #e1e4e8;'>{WebUtility.HtmlEncode(order.ProductName)}</td>
                <td align='center' style='padding:12px 10px;border-bottom:1px solid #e1e4e8;'>{order.Quantity}</td>
                <td align='right'  style='padding:12px 10px;border-bottom:1px solid #e1e4e8;'>{order.UnitPrice:C}</td>
                <td align='right'  style='padding:12px 10px;border-bottom:1px solid #e1e4e8;'>{order.TotalPrice:C}</td>
              </tr>
              <tr>
                <td colspan='3' align='right' style='padding:14px 10px;font-weight:bold;'>Order total</td>
                <td align='right' style='padding:14px 10px;font-weight:bold;font-size:16px;'>{order.TotalPrice:C}</td>
              </tr>
            </table>
          </td>
        </tr>

        <tr>
          <td style='padding:16px 32px 32px 32px;font-size:13px;line-height:1.5;color:#888;border-top:1px solid #e1e4e8;'>
            Questions about your order? Just reply to this email.<br>
            &copy; {DateTime.UtcNow.Year} MiniShop. All rights reserved.
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";

        var smtpClient = new SmtpClient("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential(fromEmail, appPassword),
            EnableSsl = true
        }
        ;

        try
        {
            smtpClient.Send(mail);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }


        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }
}