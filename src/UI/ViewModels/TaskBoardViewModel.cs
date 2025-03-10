﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Rogue.Ptb.Core;
using Rogue.Ptb.Infrastructure;
using Rogue.Ptb.UI.Behaviors;
using Rogue.Ptb.UI.Commands;
using Rogue.Ptb.UI.Views;

namespace Rogue.Ptb.UI.ViewModels
{
	public class TaskBoardViewModel : ViewModelBase, ITaskBoardViewModel, ICommandResolver
	{
		private readonly IRepositoryProvider _repositoryProvider;
		private IRepository<Task> _repository;
		private readonly IEventAggregator _bus;
		private readonly ICommandResolver _commandResolver;
		private readonly NoteDisplayer _displayer;
		private List<Task> _tasks;
		private TaskViewModel _selectedTask;

		public TaskBoardViewModel(IRepositoryProvider repositoryProvider, IEventAggregator bus, ICommandResolver commandResolver,
			NoteDisplayer displayer)
		{
			_repositoryProvider = repositoryProvider;
			_bus = bus;
			_commandResolver = commandResolver;
			_displayer = displayer;

			var tasks = new ObservableCollectionExtended<TaskViewModel>();

			var suspension = tasks.SuspendNotifications();

			var ocs = tasks.ToObservableChangeSet();
			ocs
				.ToUnit()
				.Merge(ocs.WhenAnyPropertyChanged().ToUnit())
				.Throttle(TimeSpan.FromSeconds(5))
				.Where(_ =>  _repository != null)
				.Merge(bus.Listen<TaskModified>().ToUnit())
				.ObserveOnDispatcher()
				.Do(_ => Debug.WriteLine("saving tasks"))
				.Subscribe(_ => OnSaveAllTasks(null));
			
			ocs
				.WhenPropertyChanged(t => t.IsSelected)
				.Select(pv => pv.Sender)
				.Subscribe(IsSelectedChanged);

			ocs
				.WhenPropertyChanged(c => c.State)
				.Select(pv => pv.Sender)
				.Subscribe(StateChanged);


			suspension.Dispose();

			Tasks = tasks;

			_bus.ListenOnScheduler<DatabaseChanged>(OnDatabaseChanged);
			_bus.ListenOnScheduler<CreateNewTask>(OnCreateNewTask);
			_bus.ListenOnScheduler<CreateNewSubTask>(OnCreateNewSubTask);
			_bus.ListenOnScheduler<SaveAllTasks>(OnSaveAllTasks);
			_bus.ListenOnScheduler<ReloadAllTasks>(_ => Reload());
			_bus.ListenOnScheduler<ReSort>(_ => Reorder());
			_bus.ListenOnScheduler<CollapseAll>(_ => OnCollapseAll());


			_bus.AddSource(ocs.WhenPropertyChanged(t => t.State)
				.Select(_ => new TaskStateChanged()));

			var propertyChangedOrStartup = this.WhenAnyPropertyChanged()
				.ToUnit()
				.Merge(bus.Listen<ApplicationIdle>().ToUnit());
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<EventCommand<CreateNewTask>>(_repository != null)));
			
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<EventCommand<CollapseAll>>(_repository != null)));
			
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<EventCommand<SaveAllTasks>>(_repository != null)));
			
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<EventCommand<CreateNewSubTask>>(SelectedTask != null)));
			
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<EventCommand<ReSort>>(_repository != null)));
			
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<DebugDumpImportantLinks>(_repository != null)));
			bus.AddSource(propertyChangedOrStartup
				.Select(_ => new CommandCanExecute<ExportTaskBoard>(_repository != null)));

			DragCommand = ReactiveCommand.Create<DragCommandArgs>(OnNext);
		}

		private void OnCollapseAll()
		{
			foreach (var taskViewModel in Tasks.Where(tvm => tvm.IndentLevel == 0))
			{
				if (taskViewModel.Collapsable && taskViewModel.CanCollapse)
				{
					taskViewModel.ToggleCollapseHierarchyCommand.Execute(null);
				}
			}
		}


		private void StateChanged(TaskViewModel model)
		{
			if (model.Task.Parent != null)
			{
				foreach (var vm in Tasks.Where(t => t.Task == model.Task.Parent))
				{
					vm.NotifyStateChanged();
				}
			}
		}

		private void IsSelectedChanged(TaskViewModel viewModel)
		{
			if (!viewModel.IsSelected)
			{
				return;
			}
			if (viewModel == _selectedTask)
			{
				return;
			}
			if (_selectedTask != null)
			{
				_selectedTask.IsSelected = false;
			}

			_selectedTask = viewModel;
			this.RaisePropertyChanged(vm => vm.SelectedTask);
		}

		private void OnNext(DragCommandArgs dragCommandArgs)
		{
			var target = (TaskViewModel) dragCommandArgs.DragTarget;
			var dragged = (TaskViewModel) dragCommandArgs.Dragged;

			int indexTarget = Tasks.IndexOf(target);
			int indexDragged = Tasks.IndexOf(dragged);

			if (indexTarget < 0 || indexDragged < 0)
			{
				return;
			}

			var mostImportant = indexTarget < indexDragged ? dragged : target;
			var leastImportant = mostImportant == target ? dragged : target;

			if (mostImportant.CanMakeMoreImportantThan(leastImportant))
			{
				mostImportant.IsMoreImportantThan(leastImportant);
				Reorder();
			}
		}


		public ReactiveCommand<DragCommandArgs, Unit> DragCommand { get; private set; }

		public ObservableCollectionExtended<TaskViewModel> Tasks { get; private set; }

		public TaskViewModel SelectedTask
		{
			get {
				return _selectedTask;
			}
			set {
				if (_selectedTask != null && _selectedTask.IsSelected)
				{
					_selectedTask.IsSelected = false;
				}

				this.RaiseAndSetIfChanged(ref _selectedTask, value);
				if (value != null && !value.IsSelected)
				{
					value.IsSelected = true;
				}
			}
		}

		private void OnSaveAllTasks(SaveAllTasks ignored)
		{
			var tasks = Tasks.Select(t => t.Task);
			_repository.SaveAll(tasks);
			_bus.Publish<TasksSaved>();
		}

		private void OnDatabaseChanged(DatabaseChanged databaseChanged)
		{
			_repository = NewRepository();

			Reload();

			OnCollapseAll();
			this.RaisePropertyChanged(vm => vm.Tasks);
		}

		private void Reload()
		{
			_tasks = _repository.FindAll().ToList();
			Reorder();
		}

		private void Reorder()
		{
			Tasks.Clear();


			_tasks.InPlaceSort();

			var taskViewModels = _tasks.Select(t => new TaskViewModel(t, ViewModelMapper, _displayer));
			foreach (var taskViewModel in taskViewModels)
			{
				Tasks.Add(taskViewModel);
			}
		}

		private void OnCreateNewTask(CreateNewTask ignored)
		{
			var task = new Task();

			_repository.InsertNew(task);

			var taskViewModel = new TaskViewModel(task, ViewModelMapper, _displayer);
			Tasks.Insert(0, taskViewModel);
			_tasks.Add(task);

			SelectedTask?.Deselect();

			SelectedTask = taskViewModel;
			taskViewModel.BeginEdit();
		}

		private TaskViewModel ViewModelMapper(Task task)
		{
			return Tasks.FirstOrDefault(vm => vm.Task == task);
		}

		private void OnCreateNewSubTask(CreateNewSubTask obj)
		{
			if (SelectedTask == null)
			{
				return;
			}

			if (SelectedTask.CanExpand)
			{
				SelectedTask.ToggleCollapseHierarchyCommand.Execute(null);
			}

			var newTaskViewModel = SelectedTask.CreateSubTask();
			var indexToInsert = Tasks.IndexOf(SelectedTask) + 1;
			Tasks.Insert(indexToInsert, newTaskViewModel);
			_tasks.Add(newTaskViewModel.Task);
			SelectedTask.NewChildAdded();

			var parentTask = SelectedTask;

			SelectedTask = newTaskViewModel;

			newTaskViewModel.BeginEdit();

			IDisposable subscription = null;
			subscription = SelectedTask.ObservableForProperty(vm => vm.IsEditing)
				.Where(_ => !newTaskViewModel.IsEditing)
				.SubscribeOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					SelectedTask = parentTask;
					subscription?.Dispose();
				});
		}

		private IRepository<Task> NewRepository()
		{
			if (_repository != null)
			{
				_repository.Dispose();
			}
			return _repositoryProvider.Open<Task>();
		}

		public ICommand Resolve(CommandName commandName)
		{
			return _commandResolver.Resolve(commandName);
		}

		public void Deselect()
		{
			if (Tasks == null)
			{
				return;
			}

			var taskViewModels = Tasks.Where(t => t.IsSelected);
			foreach (var taskViewModel in taskViewModels)
			{
				taskViewModel.Deselect();
			}

			SelectedTask = null;
		}

		public void StopEditingOrDeselect()
		{
			if (SelectedTask?.IsEditing == true)
			{
				SelectedTask.IsEditing = false;
			}
			else
			{
				Deselect();
			}
		}

		public void CollapseHierarchy()
		{
			if (SelectedTask == null)
			{
				return;
			}

			
		}
	}
}
