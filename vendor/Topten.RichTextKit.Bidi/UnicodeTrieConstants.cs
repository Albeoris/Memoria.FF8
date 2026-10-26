// RichTextKit
// Copyright © 2019-2020 Topten Software. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use this product except in compliance with the License. You may obtain
// a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations
// under the License.
// Modified: runtime constants only; the offline trie builder is excluded.
namespace Topten.RichTextKit
{
    internal static class UnicodeTrieBuilder
    {
        internal const int SHIFT_1 = 6 + 5;
        internal const int SHIFT_2 = 5;
        const int SHIFT_1_2 = SHIFT_1 - SHIFT_2;
        internal const int OMITTED_BMP_INDEX_1_LENGTH = 0x10000 >> SHIFT_1;
        const int CP_PER_INDEX_1_ENTRY = 1 << SHIFT_1;
        const int INDEX_2_BLOCK_LENGTH = 1 << SHIFT_1_2;
        internal const int INDEX_2_MASK = INDEX_2_BLOCK_LENGTH - 1;
        const int DATA_BLOCK_LENGTH = 1 << SHIFT_2;
        internal const int DATA_MASK = DATA_BLOCK_LENGTH - 1;
        internal const int INDEX_SHIFT = 2;
        internal const int DATA_GRANULARITY = 1 << INDEX_SHIFT;
        const int INDEX_2_OFFSET = 0;
        internal const int LSCP_INDEX_2_OFFSET = 0x10000 >> SHIFT_2;
        const int LSCP_INDEX_2_LENGTH = 0x400 >> SHIFT_2;
        const int INDEX_2_BMP_LENGTH = LSCP_INDEX_2_OFFSET + LSCP_INDEX_2_LENGTH;
        const int UTF8_2B_INDEX_2_OFFSET = INDEX_2_BMP_LENGTH;
        const int UTF8_2B_INDEX_2_LENGTH = 0x800 >> 6; // U+0800 is the first code point after 2-byte UTF-8
        internal const int INDEX_1_OFFSET = UTF8_2B_INDEX_2_OFFSET + UTF8_2B_INDEX_2_LENGTH;
        const int MAX_INDEX_1_LENGTH = 0x100000 >> SHIFT_1;
        const int BAD_UTF8_DATA_OFFSET = 0x80;
        const int DATA_START_OFFSET = 0xc0;
        const int DATA_NULL_OFFSET = DATA_START_OFFSET;
        const int NEW_DATA_START_OFFSET = DATA_NULL_OFFSET + 0x40;
        const int DATA_0800_OFFSET = NEW_DATA_START_OFFSET + 0x780;
        const int INITIAL_DATA_LENGTH = 1 << 14;
        const int MEDIUM_DATA_LENGTH = 1 << 17;
        const int MAX_DATA_LENGTH_RUNTIME = 0xffff << INDEX_SHIFT;
        const int INDEX_1_LENGTH = 0x110000 >> SHIFT_1;
        const int MAX_DATA_LENGTH_BUILDTIME = 0x110000 + 0x40 + 0x40 + 0x400;
        const int INDEX_GAP_OFFSET = INDEX_2_BMP_LENGTH;
        const int INDEX_GAP_LENGTH = ((UTF8_2B_INDEX_2_LENGTH + MAX_INDEX_1_LENGTH) + INDEX_2_MASK) & ~INDEX_2_MASK;
        const int MAX_INDEX_2_LENGTH = (0x110000 >> SHIFT_2) + LSCP_INDEX_2_LENGTH + INDEX_GAP_LENGTH + INDEX_2_BLOCK_LENGTH;
        const int INDEX_2_NULL_OFFSET = INDEX_GAP_OFFSET + INDEX_GAP_LENGTH;
        const int INDEX_2_START_OFFSET = INDEX_2_NULL_OFFSET + INDEX_2_BLOCK_LENGTH;
        const int MAX_INDEX_LENGTH = 0xffff;
    }
}
