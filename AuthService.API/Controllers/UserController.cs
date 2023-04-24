﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RideSharing.Entity;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Text;

namespace AuthService.API
{
    [Authorize(Policy = RideSharing.Entity.Constants.AuthorizationPolicy.AdminOnly)]
    [Route("api/v1/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppSettings _appSettings;

        public UserController(
            IMapper mapper,
            UserManager<User> userManager, 
            RoleManager<IdentityRole> roleManager, 
            IOptions<AppSettings> appSettings
        ) {
            _mapper = mapper;
            _userManager = userManager;
            _roleManager = roleManager;
            _appSettings = appSettings.Value;
        }

        [HttpPost("register")]
        public async Task<ActionResult<Response<RegisterDto>>> Register(RegisterDto model)
        {
            var response = new Response<RegisterDto>();
            response.Data = model;

            if (!ModelState.IsValid) // this one line with check "are all required fields of registerDto provided or not"
                throw new CustomException("Model is not valid!", 400);
            
            var user = new User
            {                    
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserName = string.IsNullOrEmpty(model.UserName) ? model.Email : model.UserName,
            };

            if (model.Password != model.ConfirmPassword) 
                throw new CustomException("Password & confirm password don't match", 400);

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                response.Message = "User registered successfully!";
                User registeredUser = await _userManager.FindByEmailAsync(user.Email);

                int addedRoleCount = await AddRolesToUser(registeredUser, model.Roles);
                response.Message += " User is added to " + addedRoleCount + " respective roles.";
            }
            else
            {
                response.Message = "Errors occured:-\n";
                foreach (var error in result.Errors)
                {
                    response.Message += error.Description + "\n";
                }
                response.Status = 400;
            }
            
            if (response.Status >= 400)
                throw new CustomException(response.Message, response.Status);

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model)
        {
            var serviceResponse = new Response<String>(); // for the token!

            User user = await _userManager.FindByEmailAsync(model.Email);
            bool isValidPassword = await _userManager.CheckPasswordAsync(user, model.Password);

            if (user == null || !isValidPassword)
                throw new CustomException("Email or password is invalid!", 400);

            var userRoles = await _userManager.GetRolesAsync(user);
            var authClaims = new List<Claim> {
                    new Claim(JwtRegisteredClaimNames.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),  
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };
            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim("role", userRole));
            }

            // preraring return token
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.JWT.Secret.ToString()));
            var token = new JwtSecurityToken(
                audience: _appSettings.JWT.ValidAudience,
                issuer: _appSettings.JWT.ValidIssuer,
                expires: DateTime.Now.AddDays(1), 
                claims: authClaims, 
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));

            serviceResponse.Data = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(serviceResponse);
        }

        [HttpGet]
        public async Task<ActionResult<Response<IEnumerable<RegisterDto>>>> GetAllUsers()
        {
            var serviceResponse = new Response<IEnumerable<RegisterDto>>();
            var users = new List<RegisterDto>();
            List<IdentityRole> dbRoles = await _roleManager.Roles.ToListAsync();

            foreach (var user in _userManager.Users)
            {
                List<string> roles = new List<string>();
                foreach (var role in dbRoles)
                {
                    if (await _userManager.IsInRoleAsync(user, role.Name))
                    {
                        roles.Add(role.Name);
                    }
                }

                users.Add(_mapper.Map<RegisterDto>(user));
                users[users.Count - 1].Roles = roles;
            }

            serviceResponse.Data = users;
            return Ok(serviceResponse);
        }

        [HttpGet("email/{email}")]
        public async Task<ActionResult<Response<RegisterDto>>> GetUserByEmail([FromRoute] string email)
        {
            var serviceResponse = new Response<RegisterDto>();

            List<IdentityRole> dbRoles = await _roleManager.Roles.ToListAsync();

            User user = await _userManager.FindByEmailAsync(email);

            if (user is null)
                throw new CustomException("No user found!", 404);

            List<string> roles = new List<string>();
            foreach (var role in dbRoles)
            {
                if (await _userManager.IsInRoleAsync(user, role.Name))
                {
                    roles.Add(role.Name);
                }
            }

            RegisterDto ret = _mapper.Map<RegisterDto>(user);
            ret.Roles = roles;

            serviceResponse.Data = ret;
            return Ok(serviceResponse);
        }

        [HttpPut("email/{email}")]
        public async Task<ActionResult<Response<RegisterDto>>> Update(RegisterDto model, [FromRoute] string email)
        {
            var serviceResponse = new Response<RegisterDto>();
            serviceResponse.Data = model;

            if (email != model.Email)
                throw new CustomException("Email in the route and email in the form body don't match!", 400);

            User user = await _userManager.FindByEmailAsync(model.Email);
            if (user is null)
                throw new CustomException("User not found!", 404);

            // updating specific properties

            if (model.FirstName is not null && user.FirstName != model.FirstName) user.FirstName = model.FirstName;
            if (model.LastName is not null && user.LastName != model.LastName) user.LastName = model.LastName;

            await _userManager.UpdateAsync(user);
            await UpdateUserRoles(user, model.Roles);

            return Ok(serviceResponse);
        }

        private async Task UpdateUserRoles(User user, IEnumerable<string> roles)
        {
            await AddRolesToUser(user, roles);
            await RemoveRolesFromUser(user, roles);
        }

        private async Task<int> AddRolesToUser(User user, IEnumerable<string> roles)
        {
            int addedRoleCount = 0;
            foreach (string role in roles)
            {
                if (string.IsNullOrWhiteSpace(role)) continue;
                if (!(await _roleManager.RoleExistsAsync(role))) continue;
                if (!(await _userManager.IsInRoleAsync(user, role.ToLower().Trim())))
                {
                    await _userManager.AddToRoleAsync(user, role.ToLower().Trim());
                    addedRoleCount++;
                }
            }
            return addedRoleCount;
        }

        private async Task<int> RemoveRolesFromUser(User user, IEnumerable<string> currentRoles)
        {
            int removedRoleCount = 0;
            var rolesInDB = await _userManager.GetRolesAsync(user);

            foreach (var role in rolesInDB)
            {
                if (currentRoles.Count(x => x == role) == 0)
                {
                    await _userManager.RemoveFromRoleAsync(user, role);
                    ++removedRoleCount;
                }
            }
            return removedRoleCount;
        }
    }

}