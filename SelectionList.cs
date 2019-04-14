namespace MultiCaret
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class SelectionList : IEnumerable<SingleSelection>
    {
        private const int DefaultGrowBy = 10;
        private SingleSelection[] items;

        public SelectionList(int initialSize = 10)
        {
            this.items = new SingleSelection[initialSize + DefaultGrowBy];
        }

        public SingleSelection this[int index]
        {
            get
            {
                if (index >= this.Count)
                    throw new IndexOutOfRangeException();

                return this.items[index];
            }
        }

        public int Count { get; private set; }

        public SingleSelection? First => this.Count == 0 ? (SingleSelection?)null : this.items[0];

        public SingleSelection? Last => this.Count == 0 ? (SingleSelection?)null : this.items[this.Count - 1];

        public void Insert(IEnumerable<SingleSelection> selections)
        {
            foreach (var selection in selections)
                this.Insert(selection);
        }

        public void InsertExpectEnd(IEnumerable<SingleSelection> selections)
        {
            foreach (var selection in selections)
                this.InsertExpectEnd(selection);
        }

        public void Clear() { this.Count = 0; }

        public SingleSelection RemoveLast()
        {
            --this.Count;
            return this.items[this.Count];
        }

        public IEnumerator<SingleSelection> GetEnumerator()
        {
            for (int i = 0; i < this.Count; ++i)
                yield return this.items[i];
        }

        IEnumerator IEnumerable.GetEnumerator() { return this.GetEnumerator(); }

        private void Insert(SingleSelection inserted)
        {
            // First check bounds
            if (this.Count == 0)
            {
                // There are no elements - simple
                this.InsertExpandIfNeeded(inserted, 0);
                return;
            }

            var insertedLocation = inserted.GetDetails();

            var startLocation = this.items[0].GetDetails();
            if (insertedLocation.IsBefore(startLocation))
            {
                // We are before the first element - simple
                this.InsertExpandIfNeeded(inserted, 0);
                return;
            }

            if (this.Count == 1)
            {
                // If there is only one element then we either overlap
                // or or after that location
                if (insertedLocation.IsAfter(startLocation))
                {
                    // We are after the single element
                    this.InsertExpandIfNeeded(inserted, this.Count);
                }
                else
                {
                    // We overlap the single element
                    this.MergeSingle(insertedLocation, new LocationWithIndex(0, startLocation));
                }

                return;
            }

            var endLocation = this.items[this.Count - 1].GetDetails();
            if (insertedLocation.IsAfter(endLocation))
            {
                // We are after the last element - simple
                this.InsertExpandIfNeeded(inserted, this.Count);
                return;
            }

            // Find the start position that either overlaps the inserted or
            // is the first location after the inserted location.
            var start = this.FindStart(
                new LocationWithIndex(0, startLocation),
                new LocationWithIndex(this.Count - 1, endLocation),
                insertedLocation);

            if (start.Selection.IsAfter(insertedLocation))
            {
                // If the start is after the selection then we need to insert
                // at the current position that start occupies
                this.InsertExpandIfNeeded(inserted, start.Index);
                return;
            }

            // Since start overlaps with inserted we need to find
            // the last selection that overlaps with the inserted
            // and perform a merge - normally we would expect the
            // overlap to be fairly small so a linier search is fine.
            var end = start;
            for (var nextIndex = start.Index + 1; nextIndex < this.Count; ++nextIndex)
            {
                var nextDetails = this.items[nextIndex].GetDetails();
                if (nextDetails.IsAfter(insertedLocation))
                    break;

                end = new LocationWithIndex(nextIndex, nextDetails);
            }

            this.Merge(insertedLocation, start, end);
        }

        private LocationWithIndex FindStart(
            LocationWithIndex minLocation,
            LocationWithIndex maxLocation,
            SingleSelectionWithLocation inserted)
        {
            if (!inserted.IsAfter(minLocation.Selection))
                return minLocation;

            var minIndex = minLocation.Index;
            var gap = maxLocation.Index - minIndex;
            while (true)
            {
                /* At this point we know that the inserted
                   location is after the min location and 
                   that is is not after the max location */

                // If the gap is 1 or 0 then the start we want
                // must be the max location
                if (gap < 2)
                    return maxLocation;

                // If the gap is more than 1 then we need to
                // reduce it, maintaining the fact that inserted
                // is after the min location and not after the
                // end location (i.e. before or overlapping the end)
                var centerIndex = minIndex + (gap / 2);
                var centerLocation = this.items[centerIndex].GetDetails();
                if (inserted.IsAfter(centerLocation))
                    minIndex = centerIndex;
                else
                    maxLocation = new LocationWithIndex(centerIndex, centerLocation);;

                gap = maxLocation.Index - minIndex;
            }
        }

        /// <summary>
        /// Insert the selection into the set expecting to insert at the end.
        /// </summary>
        /// <param name="inserted">The selection to insert.</param>
        /// <returns>True if the insertion modified the selected text,
        /// false if it did not (even if the caret position changed).</returns>
        public bool InsertExpectEnd(SingleSelection inserted)
        {
            // When we expect to be inserting at the end the fastest
            // search should normally be a linear search from the end.
            // Here we look to find either the location to insert the
            // selection at or one or more existing selections that
            // the new selection overlaps.
            var insertedLocation = inserted.GetDetails();
            LocationWithIndex? lastMatch = null;
            LocationWithIndex? firstMatch = null;
            var insertIndex = this.Count;
            while (insertIndex > 0)
            {
                var compareIndex = insertIndex - 1;
                var compareLocation = this.items[compareIndex].GetDetails();

                if (compareLocation.IsBefore(insertedLocation))
                {
                    // The new location is after the current one we are looking at so
                    // our search is over - either we have already seen any matching
                    // locations or this will be a fresh insert at this position
                    break;
                }

                if (!compareLocation.IsAfter(insertedLocation))
                {
                    // The new location overlaps with the current one we
                    // are looking at so a merge will be needed
                    if (!lastMatch.HasValue)
                        lastMatch = new LocationWithIndex(compareIndex, compareLocation);

                    firstMatch = new LocationWithIndex(compareIndex, compareLocation);
                }

                --insertIndex;
            }

            if (!lastMatch.HasValue)
            {
                // If we have not seen any matching locations then insert
                this.InsertExpandIfNeeded(inserted, insertIndex);
                return true;
            }

            // If we have seen matching locations then merge the insert with those locations
            return this.Merge(insertedLocation, firstMatch.Value, lastMatch.Value);
        }

        /// <summary>
        /// Insert the selection into the set expecting to insert at the start.
        /// </summary>
        /// <param name="inserted">The selection to insert.</param>
        /// <returns>True if inserted, false if it was a duplicate.</returns>
        public bool InsertExpectStart(SingleSelection inserted)
        {
            // When we expect to be inserting at the start the fastest
            // search should normally be a linear search from the start.
            // Here we look to find either the location to insert the
            // selection at or one or more existing selections that
            // the new selection overlaps.
            var insertedLocation = inserted.GetDetails();
            LocationWithIndex? lastMatch = null;
            LocationWithIndex? firstMatch = null;
            var insertIndex = 0;
            while (insertIndex < this.Count)
            {
                var compareLocation = this.items[insertIndex].GetDetails();

                if (compareLocation.IsAfter(insertedLocation))
                {
                    // The new location is before the current one we are looking at so
                    // our search is over - either we have already seen any matching
                    // locations or this will be a fresh insert at this position
                    break;
                }

                if (!compareLocation.IsBefore(insertedLocation))
                {
                    // The new location overlaps with the current one we
                    // are looking at so a merge will be needed
                    if (!firstMatch.HasValue)
                        firstMatch = new LocationWithIndex(insertIndex, compareLocation);

                    lastMatch = new LocationWithIndex(insertIndex, compareLocation);
                }

                ++insertIndex;
            }

            if (!lastMatch.HasValue)
            {
                // If we have not seen any matching locations then insert
                this.InsertExpandIfNeeded(inserted, insertIndex);
                return true;
            }

            // If we have seen matching locations then merge the insert with those locations
            return this.Merge(insertedLocation, firstMatch.Value, lastMatch.Value);
        }

        private bool Merge(
            SingleSelectionWithLocation inserted,
            LocationWithIndex firstExisting,
            LocationWithIndex lastExisting)
        {
            return firstExisting.Index == lastExisting.Index
                ? this.MergeSingle(inserted, firstExisting)
                : this.MergeRange(inserted, firstExisting, lastExisting);
        }

        private bool MergeSingle(SingleSelectionWithLocation inserted, LocationWithIndex existing)
        {
            if (existing.Selection.Start >= inserted.Start && existing.Selection.End <= inserted.End)
            {
                // The new selection is the same or a superset of the existing selection
                // so we can take that.
                this.items[existing.Index] = inserted.Selection;

                // The overall selection was only changed if the existing and new selections
                // exactly match.
                return existing.Selection.Start != inserted.Start
                    || existing.Selection.End != inserted.End;
            }

            if (existing.Selection.Start <= inserted.Start && existing.Selection.End >= inserted.End)
            {
                // The new location is completely covered by the existing one, we should ensure
                // that the caret is at the end specified by the insertion but this does not
                // change the overall selected text.
                this.items[existing.Index] = existing.Selection.Selection
                    .WithDirection(inserted.Selection);

                return false;
            }

            // Neither of the selections completely covers the other so create a new selection
            // the encompases both. This results in a change to the overall selection.
            this.items[existing.Index] = inserted.ExpandTo(
                existing.Selection.Start,
                existing.Selection.End);

            return true;
        }

        private bool MergeRange(
            SingleSelectionWithLocation inserted,
            LocationWithIndex firstExisting,
            LocationWithIndex lastExisting)
        {
            if (inserted.Start <= firstExisting.Selection.Start && inserted.End >= lastExisting.Selection.End)
            {
                // If the new selection completely overlaps the existing ones
                // we can simply use the new selection
                this.items[firstExisting.Index] = inserted.Selection;
            }
            else
            {
                // If the new selection does not completely overlap the old ones
                // then we need to create a new selection that is the union of
                // the old and new ones
                this.items[firstExisting.Index] = inserted.ExpandTo(
                    firstExisting.Selection.Start,
                    lastExisting.Selection.End);
            }

            // We need to move any selections after the last existing one
            // up in the list to fill the hole left by replacing multiple
            // selections with a single one and adjust the selection count
            // in the list
            var emptyGap = lastExisting.Index - firstExisting.Index;
            for (int i = lastExisting.Index + 1; i < this.Count; ++i)
                this.items[i - emptyGap] = this.items[i];

            this.Count -= emptyGap;

            // The overall selections will always have changed with a range merge
            return true;
        }

        private void InsertExpandIfNeeded(SingleSelection selection, int index)
        {
            if (this.items.Length <= this.Count)
            {
                // Expand and insert
                var newItems = new SingleSelection[this.items.Length + DefaultGrowBy];
                if (index > 0)
                    Array.Copy(this.items, newItems, index);

                newItems[index] = selection;

                if (index < this.Count - 1)
                    Array.Copy(this.items, index, newItems, index + 1, this.Count - index);

                this.items = newItems;
            }
            else
            {
                // Simple insert
                for (int i = this.Count; i > index; --i)
                    this.items[i] = this.items[i - 1];

                this.items[index] = selection;
            }

            ++this.Count;
        }

        private struct LocationWithIndex
        {
            public LocationWithIndex(int index, SingleSelectionWithLocation selection)
            {
                this.Index = index;
                this.Selection = selection;
            }

            public int Index { get; }

            public SingleSelectionWithLocation Selection { get; }
        }
    }
}
