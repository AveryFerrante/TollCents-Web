This is the same route from St. Demetrios Greek Orthodox Church, 2020 NW 21st St, Fort Worth, TX 76164 to
Home (13312 Meandering Way) with and without tolls.

Need to analyze their steps to try and help determine convergence and how I could possibly re-calculate just
some portions of the toll route to piece-meal build a route (this contains several different "toll sections" -
could I possibly tell the user exact info about these sections and let them decide to skip or take some).


Initial thoughts:
Analyze from start and find the first divergence - this has to indicate some type of toll section incoming.

Run a close (within ~0.1 miles) delta checker of start/end points

Might be best to decode the polyline information, this gives a list of very close coordinates. Somehow analyze
all of these to decide where some level of convergence has happened...