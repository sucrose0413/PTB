﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;

namespace Rogue.Ptb.Core
{
	public interface ISessionFactoryProvider
	{
		ISessionFactory GetSessionFactory();
		void CreateNewDatabase(string path);
		void OpenDatabase(string file);
	}

	public interface IRepository<T> : IDisposable
	{
		IQueryable<T> FindAll();
		void SaveAll(IEnumerable<T> items);
	}

	public interface ITasksRepository : IRepository<Task>
	{
	}

	public interface IRepositoryProvider
	{
		IRepository<T> Open<T>();
	} 

	public interface IEventAggregator
	{
		IObservable<T> Listen<T>();
		void Publish<T>(T message = default(T));
	}
}
