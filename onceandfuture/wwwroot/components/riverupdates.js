import React from 'react';
import RiverFeedUpdate from './riverfeedupdate';
import { update_key } from '../util';
import { StickyContainer } from './sticky';

const RiverUpdates = ({river, index}) => {
  const TOP_SPACE = 65;
  const SIDE_PADDING = 3;

  let style = {
    overflowX: 'hidden',
    overflowY: 'auto',
    position: 'absolute',
    top: TOP_SPACE,
    bottom: SIDE_PADDING,
    left: SIDE_PADDING,
    right: SIDE_PADDING,
  };

  let update_nodes = (river.updates || []).map(
    u => <RiverFeedUpdate
        update={u}
        mode={river.mode}
        river_index={index}
        key={update_key(u)}
      />
  );

  return <StickyContainer style={style}>{update_nodes}</StickyContainer>;
};

export default RiverUpdates;
