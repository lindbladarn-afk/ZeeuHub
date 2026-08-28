using Entities.Contracts;
using Entities.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NotificationService;
using Repository.Contracts;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Services.Application;
using WebApp.Repositories.Customers;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User")]
    public class CustomersController : BaseController
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomersController(IHttpContextAccessor contextAccessor,
                                    IApplicationUserRepository applicationUserRepository,
                                    INotificationManager notificationManager,
                                    ICustomerRepository customerRepository,
                                    IApplicationHelper applicationHelper,
                                    ApplicationDbContext context)
                                :base(contextAccessor,
                                        applicationUserRepository,
                                        notificationManager,
                                        applicationHelper,
                                        context)
        {
           _customerRepository = customerRepository;
        }

        private sealed class CustomersRequestContext
        {
            public required string ConnectionString { get; init; }
            public int? CompanyCode { get; init; }
            public required IReadOnlyList<ICustomersAutoCompleteDto> AutoCompleteCustomers { get; init; }
        }

        private async Task<OperationResult<CustomersRequestContext>> BuildRequestContextAsync()
        {
            var userObject = await GetCurrentUserAsync()
                ?? throw new InvalidOperationException("The user could not be loaded");

            var runtimeContext = await ResolveCurrentRuntimeContextAsync();
            if (runtimeContext is null)
            {
                return OperationResult<CustomersRequestContext>.Fail("Kundregistret kräver just nu en aktiv Jeeves-koppling.");
            }

            userObject.JeevesActiveCompany = runtimeContext.CompanyCode;
            userObject.CompanyId ??= runtimeContext.CompanyId;
            userObject.Email ??= runtimeContext.Email;
            userObject.PersSign ??= runtimeContext.PersSign;

            var customers = (await _customerRepository.GetAutoCompleteCustomersAsync(
                runtimeContext.ConnectionString,
                userObject.JeevesActiveCompany)).ToList();

            return OperationResult<CustomersRequestContext>.Ok(new CustomersRequestContext
            {
                ConnectionString = runtimeContext.ConnectionString,
                CompanyCode = userObject.JeevesActiveCompany,
                AutoCompleteCustomers = customers
            });
        }

        public async Task<IActionResult> Customers()
        {
            var context = await BuildRequestContextAsync();
            if (!context.Success || context.Value is null)
            {
                return View("ModuleUnavailable", BuildModuleUnavailableViewModel(context.Error));
            }

            var data = await _customerRepository.GetAllCustomersAsync(context.Value.ConnectionString, context.Value.CompanyCode);
            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> Customers(string CustomerName, string CustomerNumber)
        {
            var context = await BuildRequestContextAsync();
            if (!context.Success || context.Value is null)
            {
                return View("ModuleUnavailable", BuildModuleUnavailableViewModel(context.Error));
            }

            var data = await _customerRepository.GetAllCustomersAsync(context.Value.ConnectionString, context.Value.CompanyCode);
            return View(data);
        }


        [HttpPost]
        public async Task<IActionResult> AutoCompleteCustomers([FromBody]Auto auto)
        {
            var context = await BuildRequestContextAsync();
            if (!context.Success || context.Value is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    success = false,
                    message = string.IsNullOrWhiteSpace(context.Error)
                        ? "Kundregistret är tillfälligt otillgängligt."
                        : context.Error
                });
            }

            var customers = (from customer in context.Value.AutoCompleteCustomers
                             where customer.CompanyName.ToLower().Contains(auto.searchString.ToLower())
                             select new
                             {
                                 label = customer.CompanyName,
                                 val = customer.CustomerNumber
                             }).ToList();
            return Json(customers);
        }
        public class Auto
        {
            public string searchString { get; set; } = string.Empty;
        }

        private ModuleUnavailableViewModel BuildModuleUnavailableViewModel(string? detail)
        {
            return new ModuleUnavailableViewModel
            {
                ModuleLabel = "Kunder",
                Title = "Kundregistret är tillfälligt otillgängligt",
                Subtitle = "Kunddata läses från Jeeves i aktiv tenant.",
                State = new ModuleStateViewModel
                {
                    Title = "Jeeves är tillfälligt otillgängligt",
                    Message = "Du är fortfarande inloggad i portalen, men kundmodulen kan inte läsa tenantdata just nu.",
                    Note = string.IsNullOrWhiteSpace(detail)
                        ? "Försök igen om en stund eller byt till en modul som inte behöver live-data från Jeeves."
                        : detail,
                    Tone = "warning",
                    IconClass = "fa fa-plug",
                    ActionText = "Försök igen",
                    ActionUrl = Url.Action(nameof(Customers), "Customers")
                }
            };
        }

    }
}
