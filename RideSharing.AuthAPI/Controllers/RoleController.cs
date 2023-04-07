﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RideSharing.Entity;

namespace RideSharing.AuthAPI
{
    [Authorize(Policy = "AdminOnly")]
    [Route("api/v1/roles")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppSettings _appSettings;

        public RoleController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<AppSettings> appSettings
        )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _appSettings = appSettings.Value;
        }

        [HttpGet]
        public async Task<ActionResult<Response<IEnumerable<string>>>> GetAllRoles()
        {
            Response<IEnumerable<string>> serviceResponse = new Response<IEnumerable<string>>();
            var temp = new List<string>();
            try
            {
                foreach (var role in _roleManager.Roles)
                {
                    temp.Add(role.Name);
                }
                if (temp.Count == 0) serviceResponse.Message = "No roles found. Try inserting roles.";
            }
            catch (Exception ex)
            {
                serviceResponse.Status = 400;
                serviceResponse.Message = ex.Message;
            }
            serviceResponse.Data = temp;
            if (serviceResponse.Status < 300) return Ok(serviceResponse);
            return BadRequest(serviceResponse);
        }

        [HttpPost]
        public async Task<ActionResult<Response<RoleDto>>> CreateRole([FromBody] RoleDto newRole)
        {
            if (String.IsNullOrEmpty(newRole.Name))
                throw new CustomException("Model is invalid!", 400);

            var serviceResponse = new Response<RoleDto>();
            serviceResponse.Data = newRole;

            var roleInDB = await _roleManager.FindByNameAsync(newRole.Name.Trim().ToLower());
            if (roleInDB is not null)
                throw new CustomException("Role already exists!", 405);

            await _roleManager.CreateAsync(new IdentityRole() { Name = newRole.Name.Trim().ToLower() });
            serviceResponse.Message = "New role created!";

            return Created("example.com", serviceResponse);
        }

        [HttpDelete]
        public async Task<ActionResult<Response<RoleDto>>> DeleteRole([FromBody] RoleDto role)
        {
            if (String.IsNullOrEmpty(role.Name))
                throw new CustomException("Model is invalid!", 400);

            var serviceResponse = new Response<RoleDto>();
            serviceResponse.Data = role;

            var roleInDB = await _roleManager.FindByNameAsync(role.Name);
            if (roleInDB is null)
                throw new CustomException("Role not found", 400);

            var res = await _roleManager.DeleteAsync(roleInDB);

            return Ok(serviceResponse);
        }
    }
}
