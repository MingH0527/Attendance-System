using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using WebAndMobile.Models;
using Microsoft.AspNetCore.Authorization;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly IConfiguration _configuration;

    public PaymentController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private List<FeeItem> GetFeeItems()
    {
        return new List<FeeItem>
        {
            new FeeItem
            {
                Id = 1,
                Name = "Tuition Fee",
                Description = "Monthly school tuition fee",
                Amount = 500.00m,
                Category = "Tuition",
                AmountIsFixed = true
            },

            new FeeItem
            {
                Id = 2,
                Name = "Library Fee",
                Description = "Library membership and maintenance fee",
                Amount = 50.00m,
                Category = "Library",
                AmountIsFixed = true
            },

            new FeeItem
            {
                Id = 3,
                Name = "Hostel Fee",
                Description = "Monthly student hostel fee",
                Amount = 300.00m,
                Category = "Hostel",
                AmountIsFixed = true
            },

            new FeeItem
            {
                Id = 4,
                Name = "Books and Materials",
                Description = "Purchase books and learning materials",
                Amount = 10.00m,
                Category = "Books",
                AmountIsFixed = false
            }
        };
    }

    [HttpGet]
    public IActionResult SelectFee()
    {
        var fees = GetFeeItems();

        return View(fees);
    }

    [HttpGet]
    public IActionResult Pay(int feeId)
    {
        var selectedFee = GetFeeItems()
            .FirstOrDefault(f => f.Id == feeId);

        if (selectedFee == null)
        {
            return NotFound();
        }

        ViewBag.PublishableKey =
            _configuration["Stripe:PublishableKey"];

        return View(selectedFee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession(
        int feeId,
        decimal? customAmount)
    {
        var selectedFee = GetFeeItems()
            .FirstOrDefault(f => f.Id == feeId);

        if (selectedFee == null)
        {
            return NotFound();
        }

        decimal finalAmount;

        if (selectedFee.AmountIsFixed)
        {
            finalAmount = selectedFee.Amount;
        }
        else
        {
            if (customAmount == null ||
                customAmount <= 0)
            {
                ModelState.AddModelError(
                    nameof(customAmount),
                    "Please enter a valid amount."
                );

                ViewBag.PublishableKey =
                    _configuration["Stripe:PublishableKey"];

                return View(
                    "Pay",
                    selectedFee
                );
            }

            finalAmount = customAmount.Value;
        }

        if (finalAmount <= 0)
        {
            return BadRequest(
                "The payment amount must be greater than zero."
            );
        }

        long amountInSen = Convert.ToInt64(
            decimal.Round(
                finalAmount * 100,
                0,
                MidpointRounding.AwayFromZero
            )
        );

        string domain =
            $"{Request.Scheme}://{Request.Host}";

        var options = new SessionCreateOptions
        {
            Mode = "payment",

            SuccessUrl =
                $"{domain}/Payment/Success" +
                $"?session_id={{CHECKOUT_SESSION_ID}}",

            CancelUrl =
                $"{domain}/Payment/Cancel",

            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,

                    PriceData =
                        new SessionLineItemPriceDataOptions
                        {
                            Currency = "myr",

                            UnitAmount = amountInSen,

                            ProductData =
                                new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = selectedFee.Name,

                                    Description =
                                        selectedFee.Description
                                }
                        }
                }
            },

            Metadata = new Dictionary<string, string>
            {
                { "FeeId", selectedFee.Id.ToString() },
                { "FeeName", selectedFee.Name }
            }
        };

        var service = new SessionService();

        Session session;

        try
        {
            session = await service.CreateAsync(options);
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest(
                $"Stripe error: {ex.Message}"
            );
        }

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            return BadRequest(
                "Stripe did not return a checkout URL."
            );
        }

        return Redirect(session.Url);
    }

    [HttpGet]
    public async Task<IActionResult> Success(
        string? session_id)
    {
        if (string.IsNullOrWhiteSpace(session_id))
        {
            return RedirectToAction(
                nameof(SelectFee)
            );
        }

        session_id = session_id.Trim(
            '{',
            '}',
            ' ',
            '"'
        );

        if (!session_id.StartsWith(
                "cs_",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                "Invalid Stripe Checkout Session ID."
            );
        }

        var service = new SessionService();

        Session session;

        try
        {
            session = await service.GetAsync(
                session_id
            );
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest(
                $"Unable to retrieve Stripe payment: {ex.Message}"
            );
        }

        if (session.PaymentStatus != "paid")
        {
            return RedirectToAction(
                nameof(Cancel)
            );
        }

        ViewBag.SessionId =
            session.Id;

        ViewBag.PaymentStatus =
            session.PaymentStatus;

        ViewBag.CustomerEmail =
            session.CustomerDetails?.Email;

        ViewBag.AmountTotal =
            session.AmountTotal.HasValue
                ? session.AmountTotal.Value / 100m
                : 0m;

        ViewBag.Currency =
            session.Currency?.ToUpperInvariant();

        return View(session);
    }

    [HttpGet]
    public IActionResult Cancel()
    {
        return View();
    }
}