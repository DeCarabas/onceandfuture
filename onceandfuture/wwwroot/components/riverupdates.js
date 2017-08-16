import React from 'react';
import RiverFeedUpdate from './riverfeedupdate';
import { update_key } from '../util';

const RiverUpdates = ({ river, index }) => {
  let style = {
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
