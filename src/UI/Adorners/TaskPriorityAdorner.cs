﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Rogue.Ptb.Infrastructure;

namespace Rogue.Ptb.UI.Adorners
{
	public class TaskPriorityAdorner : TaskboardAdornerBase
	{

		private readonly Pen _pen = new Pen(Brushes.Blue, 0.75);

		public TaskPriorityAdorner(UIElement adornedElement) : base(adornedElement)
		{
		}

		protected override void DoRender(DrawingContext drawingContext)
		{
			var items = from vm in GetViewModels()
			            where vm.IsVisible
			            let fe = FrameworkElementFromItem(vm)
			            let border = FindBorder(fe)
			            select new {Border = border, Viewmodel = vm};


			items = items.ToArray();

			var currentItem = items.FirstOrDefault();
			foreach (var nextItem in items.Skip(1))
			{
				if (currentItem.Viewmodel.Task.LessImportantTasks.Contains(nextItem.Viewmodel.Task))
				{
					RenderImportanceArrow(drawingContext, currentItem.Border, nextItem.Border);
				}
				currentItem = nextItem;
			}
		}

		private void RenderImportanceArrow(DrawingContext dc, FrameworkElement mostImportant, FrameworkElement leastImportant)
		{
			var startPoint = FindPoint(mostImportant, new Point(mostImportant.ActualWidth/2, mostImportant.ActualHeight));
			var endPoint = FindPoint(leastImportant, new Point(leastImportant.ActualWidth/2, 0));


			var vector = Point.Subtract(endPoint, startPoint);

			var midPoint = Point.Add(startPoint, Vector.Multiply(vector, 0.66));

			Transform arrowTransform = Transform.Identity;
			double angle;
			if (Math.Abs((angle = Vector.AngleBetween(vector, new Vector(0, -1))) - 0) > double.Epsilon)
			{
				arrowTransform = new RotateTransform(angle, midPoint.X, midPoint.Y);
			}

			var line = BuildPath.From(startPoint).LineTo(endPoint.X, endPoint.Y).Build();
			var arrow = BuildPath.From(midPoint.X - 3, midPoint.Y - 3).LineTo(midPoint.X, midPoint.Y)
				.LineTo(midPoint.X + 3, midPoint.Y - 3).Build();

			dc.DrawGeometry(null, _pen, line);

			dc.PushTransform(arrowTransform);

			dc.DrawGeometry(null, _pen, arrow);

			dc.Pop();
		}
	}
}