﻿using RideSharing.Domain.Entities;

namespace RideSharing.Application.TripUseCase.Commands.TripRequestCommand
{
	public record struct TripRequestCommandResponseDto(
		Guid TripId,
		Guid CustomerId,
		Tuple<double, double> Source,
		Tuple<double, double> Destination,
		string TripStatus,
		string CabType
		)
	{
		public TripRequestCommandResponseDto(Trip trip)
			: this(trip.Id,
				  trip.CustomerId,
				  new Tuple<double, double>(trip.Source.X, trip.Source.Y),
				  new Tuple<double, double>(trip.Destination.X, trip.Destination.Y),
				  trip.TripStatus.ToString(),
				  trip.CabType.ToString())
		{

		}
	}
}
