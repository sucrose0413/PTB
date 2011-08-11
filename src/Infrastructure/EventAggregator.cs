﻿using System;
using System.Collections.Generic;

namespace Rogue.Ptb.Infrastructure
{
	public class EventAggregator : IEventAggregator
	{
		private readonly Dictionary<Type, object> _events = new Dictionary<Type, object>();

		public IObservable<T> Listen<T>()
		{
			var subject = GetSubject<T>();
			return subject;
		}

		public void Publish<T>(T message = default(T))
		{
			var subject = GetSubject<T>();
			subject.OnNext(message);
		}

		public void AddSource<T>(IObservable<T> observable)
		{
			var subject = GetSubject<T>();
			observable.Subscribe(subject);
		}

		private Subject<T> GetSubject<T>()
		{
			object subject;
			lock (_events)
			{
				if (!_events.TryGetValue(typeof (T), out subject))
				{
					subject = new Subject<T>();
					_events[typeof (T)] = subject;
				}
			}

			return (Subject<T>)subject;
		}
	}

}
