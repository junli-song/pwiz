//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "ProteinListCache.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


namespace std {

ostream& operator<< (ostream& os, const ProteinListCache::CacheType& cache)
{
    os << "Protein cache indices (from MRU to LRU):";
    for (ProteinListCache::CacheType::const_iterator itr = cache.begin(); itr != cache.end(); ++itr)
        os << " " << itr->second->index;
    return os;
}

} // namespace std


void testModeOff()
{
    // initialize list
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));

    // access a series of proteins and make sure the cache behaves appropriately:
    // in off mode, the cache should always be empty

    ProteinPtr s;

    ProteinListCache slc(sl, ProteinListCacheMode_Off, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());

    s = slc.protein(0, false);
    s = slc.protein(1, true);
    unit_assert(s->description == "1");
    unit_assert(s->sequence() == "DEF");
    s = slc.protein(2, false);
    s = slc.protein(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
}


void testModeMetaDataOnly()
{
    // initialize list
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));

    // access a series of proteins and make sure the cache behaves appropriately:
    // in metadata-only mode, entries in the cache should:
    // - always have metadata
    // - never have sequences

    ProteinPtr s;

    ProteinListCache slc(sl, ProteinListCacheMode_MetaDataOnly, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());
    unit_assert(cache.max_size() == 2);

    s = slc.protein(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(!cache.empty());
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 0);
    unit_assert(cache.mru().second->description == "0");
    unit_assert(cache.mru().second->sequence() == "ABC");

    // with-sequence access should return the sequence, but only cache the metadata
    s = slc.protein(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(cache.mru().second->sequence().empty());
    unit_assert(cache.lru().second->index == 0);

    s = slc.protein(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(cache.lru().second->index == 1);

    s = slc.protein(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 3);
    unit_assert(cache.mru().second->sequence().empty());
    unit_assert(cache.lru().second->index == 2);

    s = slc.protein(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(cache.mru().second->sequence().empty());
    unit_assert(cache.lru().second->index == 3);
}


void testModeMetaDataAndSequence()
{
    // initialize list
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));

    // access a series of proteins and make sure the cache behaves appropriately:
    // in metadata-and-sequence mode, entries in the cache should:
    // - always have metadata
    // - always have sequences

    ProteinPtr s;

    ProteinListCache slc(sl, ProteinListCacheMode_MetaDataAndSequence, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());
    unit_assert(cache.max_size() == 2);

    // metadata-only access should not affect the cache
    s = slc.protein(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert(cache.size() == 0);

    s = slc.protein(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(cache.mru().second->description == "1");
    unit_assert(cache.mru().second->sequence() == "DEF");

    s = slc.protein(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(!cache.mru().second->sequence().empty());

    s = slc.protein(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 3);
    unit_assert(!cache.mru().second->sequence().empty());
    unit_assert(cache.lru().second->index == 1);
    unit_assert(!cache.mru().second->sequence().empty());

    s = slc.protein(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(!cache.mru().second->sequence().empty());
    unit_assert(cache.lru().second->index == 3);
    unit_assert(!cache.mru().second->sequence().empty());
}


void test()
{
    testModeOff();
    testModeMetaDataOnly();
    testModeMetaDataAndSequence();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
    }

    return 1;
}
