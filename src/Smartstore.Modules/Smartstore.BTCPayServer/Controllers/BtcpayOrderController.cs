using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;
using Smartstore.Web.Controllers;

namespace Smartstore.BTCPayServer.Controllers
{
    [Route("btcpayserver/order")]
    public class BtcPayOrderController : PublicController
    {
        private readonly SmartDbContext _db;

        public BtcPayOrderController(
            SmartDbContext db)
        {
            _db = db;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var orderId = (await _db.Orders.SingleOrDefaultAsync(order => order.OrderGuid == id))?.Id;
            if (orderId is null)
            {
                return NotFound();
            }

            return RedirectToAction("Details", "Order", new {id = orderId});
        }
    }
}