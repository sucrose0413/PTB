﻿using NHibernate;
using Rogue.Ptb.Core.Repositories;
using StructureMap;

namespace Rogue.Ptb.Core
{
	public class CoreRegistry : Registry
	{
		public CoreRegistry()
		{
			Scan(scanner =>
				{
					scanner.WithDefaultConventions();
					scanner.TheCallingAssembly();
					scanner.AddAllTypesOf<IDatabaseInitializer>();
				});


			For<ISession>().Use(c => c.GetInstance<ISessionFactoryProvider>().GetSessionFactory().OpenSession());
			ForSingletonOf<ISessionFactoryProvider>().Use<SessionFactoryProvider>();
			For(typeof (IRepository<>)).Use(typeof (RepositoryBase<>));
			// For<IDatabaseServices>().Use<SqlCeDatabaseServices>();
		}
	}
}
