import React from 'react';
import RiverFeedUpdate from './riverfeedupdate';
import { update_key } from '../util';
import {
  SIZE_SPACER_WIDTH,
  SIZE_UPDATE_TOP,
} from './style';

const RiverUpdates = ({ river, index }) => {
  const TOP_SPACE = SIZE_UPDATE_TOP;
  const SIDE_PADDING = SIZE_SPACER_WIDTH;

  let style = {
    overflowX: 'hidden',
    overflowY: 'auto',
    marginTop: SIDE_PADDING,
    marginBottom: SIDE_PADDING,
    marginLeft: SIDE_PADDING,
    marginRight: SIDE_PADDING,
    height: '100%',
    flex: '1 1 auto',
    display: 'flex',
    flexDirection: 'column',
  };

  let update_nodes = (river.updates || []).map(
    u => <div style={{ flex: '1 1 auto' }}>
      <RiverFeedUpdate
        update={u}
        mode={river.mode}
        river_index={index}
        key={update_key(u)}
      />
    </div>
  );

  return <div style={style}>{update_nodes}</div>;
};

export default RiverUpdates;
