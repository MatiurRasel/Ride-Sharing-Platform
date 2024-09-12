﻿using RideSharing.Application.Abstractions;
using RideSharing.Domain.Entities;

namespace RideSharing.InternalAPI.Controllers
{
	public class CabController : BaseController<Cab>
	{
		public CabController(IBaseRepository<Cab> repository) : base(repository)
		{

		}
	}
}
