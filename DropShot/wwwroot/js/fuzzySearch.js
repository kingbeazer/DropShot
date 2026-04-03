window.FuzzySearch = (() => {
    let fuse = null;

    return {
        initialize(players) {
            fuse = new Fuse(players, {
                keys: ['displayName', 'firstName', 'lastName'],
                threshold: 0.4,
                includeScore: true,
                shouldSort: true
            });
        },

        search(query, limit) {
            if (!fuse || !query) return [];
            return fuse.search(query, { limit })
                .map(r => ({
                    playerId: r.item.playerId,
                    displayName: r.item.displayName,
                    firstName: r.item.firstName,
                    lastName: r.item.lastName,
                    isLight: r.item.isLight,
                    score: r.score
                }));
        },

        updateCollection(players) {
            this.initialize(players);
        }
    };
})();
