﻿using CSharpFunctionalExtensions;
using MediatR;
using RideSharing.Application.Abstractions;
using RideSharing.Common.MessageQueues.Abstractions;
using RideSharing.Domain.Entities;

namespace RideSharing.Application.TripRequests.Commands.TripRequests
{
	public class TripRequestCommandHandler(
		IUnitOfWork unitOfWork,
		ITripRequestEventMessageBus messageBus)
		: IRequestHandler<TripRequestCommandDto, Result<string>>
	{
		public async Task<Result<string>> Handle(TripRequestCommandDto model, CancellationToken cancellationToken)
		{
			// Step 1: check customer exists
			var customerInDB = await unitOfWork.CustomerRepository.FindByIdAsync(model.CustomerId);

			if (customerInDB == null)
			{
				return Result.Failure<string>("Customer is not found.");
			}

			// Step 2: check customer has ongoing trip requests
			var requestedTrip = await unitOfWork.TripRequestRepository.GetActiveTripRequestForCustomer(model.CustomerId);

			if (requestedTrip != null)
			{
				return Result.Failure<string>("Customer has already a requested trip.");
			}

			// Step 3: check customer has ongoing trips
			var unfinishedTrip = await unitOfWork.TripRepository.GetActiveTripForCustomer(model.CustomerId);

			if (unfinishedTrip != null)
			{
				return Result.Failure<string>("Customer has already an ongoing trip.");
			}

			// Step 4: create trip request entity
			Result<TripRequest> tripRequest = new TripRequest
			{
				CustomerId = model.CustomerId,
				SourceX = model.Source.Item1,
				SourceY = model.Source.Item2,
				DestinationX = model.Destination.Item1,
				DestinationY = model.Destination.Item2,
				CabType = model.CabType,
				PaymentMethod = model.PaymentMethod,
			};

			if (tripRequest.IsFailure)
			{
				return Result.Failure<string>("Please provide valid data.");
			}

			// Step 5: perform db operations
			try
			{
				// Note: log table is inserted from database triggers, not api

				await unitOfWork.TripRequestRepository.CreateAsync(tripRequest.Value);

				// call UoW to save the changes in db.
				var result = await unitOfWork.SaveChangesAsync();

				if (result.IsFailure)
				{
					return Result.Failure<string>(result.Error);
				}

				// Note: this method call is not intentionally awaited!
				var messageDto = tripRequest.Value.GetTripRequestDto();

				messageBus.PublishAsync(messageDto);

				// Step 5: return response

				return Result.Success(tripRequest.Value.Id);
			}
			catch (Exception ex)
			{
				return Result.Failure<string>($"Failed with error: {ex.Message}");
			}
		}
	}
}
