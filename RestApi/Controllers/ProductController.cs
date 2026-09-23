using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestApi.Data;
using RestApi.Models;
using System.Linq;

namespace RestApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Product")]
    public class ProductController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Create(Product product)
        {
            _context.Products.Add(product);
            _context.SaveChanges();
            return Ok(product);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, Product product)
        {
            var existingProduct = _context.Products.Find(id);
            if (existingProduct == null)
            {
                return NotFound();
            }
            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            _context.SaveChanges();
            return Ok(existingProduct);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null)
            {
                return NotFound();
            }
            _context.Products.Remove(product);
            _context.SaveChanges();
            return Ok();
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var products = _context.Products.ToList();
            return Ok(products);
        }
    }
}