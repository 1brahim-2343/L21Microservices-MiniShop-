namespace OrderService.API.Models;

public class CreateOrderRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
    public required string Email { get; set; }
}