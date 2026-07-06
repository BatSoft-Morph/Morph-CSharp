using System;
using System.Collections.Generic;

namespace Morph.Manager.ViewModels
{
    /// <summary>One sortable column: how to compare two rows by it, and its header caption.</summary>
    public class SortColumn<TRow>
    {
        public SortColumn(string id, string caption, Comparison<TRow> compare)
        {
            Id = id;
            Caption = caption;
            _compare = compare;
        }

        private readonly Comparison<TRow> _compare;

        public string Id { get; }
        public string Caption { get; }

        public int Compare(TRow left, TRow right)
            => _compare(left, right);
    }

    /// <summary>
    /// Remembers which column a list is sorted by and in which direction, and orders rows
    /// accordingly.  Selecting the current column reverses direction; selecting any other column
    /// switches to it, ascending.  A fixed tie-breaker column keeps the order deterministic
    /// within equal keys.
    /// </summary>
    public class ListSorter<TRow>
    {
        public ListSorter(string tieBreakerId, params SortColumn<TRow>[] columns)
        {
            foreach (SortColumn<TRow> column in columns)
                _columns.Add(column.Id, column);
            _tieBreaker = _columns[tieBreakerId];
            _current = _tieBreaker;
        }

        private readonly Dictionary<string, SortColumn<TRow>> _columns = new Dictionary<string, SortColumn<TRow>>();
        private readonly SortColumn<TRow> _tieBreaker;
        private SortColumn<TRow> _current;
        private bool _ascending = true;

        /// <summary>The current column reverses direction; any other becomes the ascending sort.</summary>
        public void SortBy(string columnId)
        {
            SortColumn<TRow> column = _columns[columnId];
            if (column == _current)
                _ascending = !_ascending;
            else
            {
                _current = column;
                _ascending = true;
            }
        }

        /// <summary>Orders the rows in place, by the current column and direction.</summary>
        public void Sort(List<TRow> rows)
            => rows.Sort(Compare);

        private int Compare(TRow left, TRow right)
        {
            int result = _current.Compare(left, right);
            if (!_ascending)
                result = -result;
            if (result != 0 || _current == _tieBreaker)
                return result;
            return _tieBreaker.Compare(left, right);
        }

        /// <summary>The column's caption, with a direction glyph when it is the active sort column.</summary>
        public string Caption(string columnId)
        {
            SortColumn<TRow> column = _columns[columnId];
            if (column != _current)
                return column.Caption;
            return column.Caption + (_ascending ? "  ▲" : "  ▼");
        }
    }
}
