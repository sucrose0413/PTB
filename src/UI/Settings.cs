﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using DynamicData.Kernel;
using ReactiveUI;
using Rogue.Ptb.Core;
using Rogue.Ptb.Infrastructure;

namespace Rogue.Ptb.UI
{
	public class Settings : ISettings
	{
		private readonly Properties.Settings _settings;
		private readonly IEventAggregator _eventAggregator;

		public Settings(Properties.Settings settings, IEventAggregator eventAggregator)
		{
			_settings = settings;
			_eventAggregator = eventAggregator;
		}

		public void Start()
		{
			_eventAggregator.ListenOnScheduler<DatabaseChanged>(OnDatabaseChanged);
		}

		private void OnDatabaseChanged(DatabaseChanged databaseChanged)
		{
			if (_settings.LastRecentlyUsedTaskboards == null)
			{
				_settings.LastRecentlyUsedTaskboards = new StringCollection();
			}

			var index = LastRecentlyUsedTaskBoards
				.Select((Taskboard, Index) => new{Taskboard, Index})
				.FirstOrDefault(tb => tb.Taskboard.Equals(databaseChanged.Path, StringComparison.CurrentCultureIgnoreCase));
			if (index != null)
			{
				_settings.LastRecentlyUsedTaskboards.RemoveAt(index.Index);
			}
			_settings.LastRecentlyUsedTaskboards.Insert(0, databaseChanged.Path);
			_settings.Save();
		}

		public IEnumerable<string> LastRecentlyUsedTaskBoards =>
			(_settings.LastRecentlyUsedTaskboards ?? 
			 (_settings.LastRecentlyUsedTaskboards = new StringCollection())).Cast<string>();
	}
}
