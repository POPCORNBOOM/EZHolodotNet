using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EZHolodotNet.Core
{
    public class UndoRedoManager
    {
        private class Operation
        {
            public List<Point> Points { get; }
            public bool IsAddOperation { get; }

            public Operation(List<Point> points, bool isAddOperation)
            {
                Points = new List<Point>(points);
                IsAddOperation = isAddOperation;
            }
        }

        private readonly Stack<Operation> _undoStack = new Stack<Operation>();
        private readonly Stack<Operation> _redoStack = new Stack<Operation>();
        private readonly List<Point> _points;

        public UndoRedoManager(List<Point> points)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
        }

        public void AddOperation(List<Point> points, bool isAddOperation)
        {
            // 执行操作
            if (isAddOperation)
                _points.AddRange(points);
            else
                _points.RemoveAll(p => points.Contains(p));

            // 记录操作
            _undoStack.Push(new Operation(points, isAddOperation));
            _redoStack.Clear(); // 每次新操作后清空 redo 栈
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var operation = _undoStack.Pop();
            if (operation.IsAddOperation)
                _points.RemoveAll(p => operation.Points.Contains(p));
            else
                _points.AddRange(operation.Points);

            _redoStack.Push(operation);
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var operation = _redoStack.Pop();
            if (operation.IsAddOperation)
                _points.AddRange(operation.Points);
            else
                _points.RemoveAll(p => operation.Points.Contains(p));

            _undoStack.Push(operation);
        }
    }
}
