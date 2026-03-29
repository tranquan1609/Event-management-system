using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DACSWEBSK.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DACSWEBSK.Areas.Identity.Pages.Account
{
    public class ConfirmCodeModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ConfirmCodeModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string Message { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Mã xác nhận")]
            public string Code { get; set; }
        }

        public void OnGet(string email)
        {
            Input = new InputModel { Email = email };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Message = "Vui lòng nhập đầy đủ thông tin.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                Message = "Không tìm thấy tài khoản.";
                return Page();
            }

            if (user.EmailConfirmationCode == Input.Code)
            {
                user.IsEmailConfirmed = true;
                user.EmailConfirmationCode = null;
                await _userManager.UpdateAsync(user);

                // Không tự động đăng nhập ở đây
                TempData["RegisterSuccess"] = "Bạn đã đăng ký thành công, hãy đăng nhập tài khoản và trải nghiệm!";
                return RedirectToPage("Login");
            }
            else
            {
                Message = "Mã xác nhận không đúng. Vui lòng kiểm tra lại.";
                return Page();
            }
        }
    }
}
