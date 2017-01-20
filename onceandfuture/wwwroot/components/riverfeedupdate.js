import React from 'react';
import { connect } from 'react-redux';
import { expandFeedUpdate, collapseFeedUpdate } from '../actions';
import { update_key } from '../util';
import RiverFeedUpdateTitle from './riverfeedupdatetitle';
import RiverItem from './riveritem';

const MoreBox = ({update, river_index, expand, collapse}) => {
  if (update.item.length > 3) {
    const moreStyle = {
       textAlign: 'right',
       cursor: 'pointer',
    };
    if (!update.expanded) {
      const click = expand(river_index, update_key(update));
      return <p style={moreStyle} onClick={click}>See more...</p>;
    }
    //  else {
    //   const click = collapse(river_index, update_key(update));
    //   return <p style={moreStyle} onClick={click}>Less...</p>;
    // }
  }

  return <p />;
};

const RiverFeedUpdateBase = ({update, mode, river_index, expand, collapse}) => {
  const style = {
    margin: 3,
  };

  const innerStyle = {
    marginLeft: 10,
  };

  const items = update.expanded ? update.item : update.item.slice(0, 3);
  const ris = items.map((item, index) => <RiverItem item={item} mode={mode} key={'i'+index} />);
  return(
    <div style={style}>
      <RiverFeedUpdateTitle update={update} />
      <div style={innerStyle}>
        { ris }
        <MoreBox
          update={update}
          river_index={river_index}
          expand={expand}
          collapse={collapse}
          />
      </div>
    </div>
  );
};

const mapStateToProps = () => { return {}; };
const mapDispatchToProps = (dispatch) => {
  return {
    expand: (river_index, update_key) =>
      () => dispatch(expandFeedUpdate(river_index, update_key)),
    collapse: (river_index, update_key) =>
      () => dispatch(collapseFeedUpdate(river_index, update_key)),
  };
};

const RiverFeedUpdate =
  connect(mapStateToProps, mapDispatchToProps)(RiverFeedUpdateBase);

export default RiverFeedUpdate;
