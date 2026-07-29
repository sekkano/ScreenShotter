using Xunit;

namespace ScreenShotter.Tests;

public class UndoRedoStackTests
{
    private sealed class TestAction : IUndoAction
    {
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }
        public string Description { get; }

        public TestAction(string description = "test")
        {
            Description = description;
        }

        public void Undo() => UndoCalls++;
        public void Redo() => RedoCalls++;
    }

    [Fact]
    public void Push_ThenUndoRedo_RestoresStackState()
    {
        var stack = new UndoRedoStack();
        var a = new TestAction("A");

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);

        stack.Push(a);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal("A", stack.NextUndoDescription);

        Assert.True(stack.Undo());
        Assert.Equal(1, a.UndoCalls);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
        Assert.Equal("A", stack.NextRedoDescription);

        Assert.True(stack.Redo());
        Assert.Equal(1, a.RedoCalls);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Push_ClearsRedoBranch()
    {
        var stack = new UndoRedoStack();
        var first = new TestAction("first");
        var second = new TestAction("second");

        stack.Push(first);
        stack.Undo();
        Assert.True(stack.CanRedo);

        stack.Push(second);
        Assert.False(stack.CanRedo);
        Assert.Equal("second", stack.NextUndoDescription);
        Assert.True(stack.Undo());
        Assert.Equal(1, second.UndoCalls);
        // first was already undone once, then dropped from redo when second was pushed
        Assert.Equal(1, first.UndoCalls);
        Assert.False(stack.CanUndo); // only second remains undoing; first is gone
    }

    [Fact]
    public void MaxDepth_DropsOldestActions()
    {
        var stack = new UndoRedoStack(maxDepth: 2);
        var a = new TestAction("a");
        var b = new TestAction("b");
        var c = new TestAction("c");

        stack.Push(a);
        stack.Push(b);
        stack.Push(c);

        Assert.Equal(2, stack.UndoCount);
        Assert.True(stack.Undo()); // undoes c
        Assert.True(stack.Undo()); // undoes b
        Assert.False(stack.Undo()); // a was dropped
        Assert.Equal(1, c.UndoCalls);
        Assert.Equal(1, b.UndoCalls);
        Assert.Equal(0, a.UndoCalls);
    }

    [Fact]
    public void BoxTransformState_EqualsState_DetectsChanges()
    {
        var a = new BoxTransformState
        {
            Location = new Point(1, 2),
            Size = new Size(10, 20),
            Zoom = 1.0,
            Pan = new Point(0, 0)
        };
        var same = a;
        var moved = a;
        moved.Location = new Point(3, 4);

        Assert.True(a.EqualsState(same));
        Assert.False(a.EqualsState(moved));
    }
}
