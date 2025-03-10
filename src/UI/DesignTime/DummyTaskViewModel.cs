﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rogue.Ptb.Core;

namespace Rogue.Ptb.UI.DesignTime
{
	public class DummyTaskViewModel
	{
		private TaskState _state;

		public DummyTaskViewModel()
		{
			Title = "Hello world, all of you";
			State = TaskState.InProgress;

		}

		public string Title { get; set; }


		public DummyTaskViewModel(TaskState state)
		{
			_state = state;
		}

		public TaskState State
		{
			get { return _state; }
			set { _state = value; }
		}
	}
}
