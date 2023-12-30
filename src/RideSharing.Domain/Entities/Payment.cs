﻿using RideSharing.Domain.Enums;

namespace RideSharing.Domain.Entities
{
	public class Payment : BaseEntity
	{
		public long TripId { get; protected set; }
		public virtual Trip Trip { get; protected set; }
		public PaymentMethod PaymentMethod { get; protected set; }
		public PaymentStatus PaymentStatus { get; protected set; }
		public long Amount { get; protected set; }

		public static Payment Create(long Id, long TripId, PaymentMethod Method, PaymentStatus Status, long Amount)
		{
			Payment payment = new()
			{
				Id = Id,
				TripId = TripId,
				PaymentMethod = Method,
				PaymentStatus = Status,
				Amount = Amount,
			};

			return payment;
		}
	}
}