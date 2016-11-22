var React = require('react'); // N.B. Still need this because JSX.
import RiverFeedUpdate from './riverfeedupdate'
import { update_key } from '../util'

const RiverUpdates = ({river, index}) => {
  const TOP_SPACE = 65;
  const SIDE_PADDING = 3;

  let style = {
    maxHeight: '100%',
    overflowX: 'hidden',
    overflowY: 'auto',
    position: 'absolute',
    top: TOP_SPACE,
    bottom: SIDE_PADDING,
    left: SIDE_PADDING,
    right: SIDE_PADDING,
  };

  let updates = river.updates || [];
  return <div style={style}>
    {
      updates.map(u =>
        <RiverFeedUpdate
          update={u}
          mode={river.mode}
          river_index={index}
          key={update_key(u)}
          />)
      }
  </div>;
}

export default RiverUpdates;
