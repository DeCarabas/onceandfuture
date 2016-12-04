var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux';
import {
  RIVER_COLUMN_BACKGROUND_COLOR,
  COLOR_VERY_DARK
} from './style';
import RiverBalloon from './riverballoon';
import RiverSettings from './riversettings';
import RiverProgress from './riverprogress';
import RiverTitle from './rivertitle';
import RiverUpdates from './riverupdates';
import {
  dropRiver,
  showRiverSettings,
  hideRiverSettings,
  dismissRiverBalloon,
  riverGetFeedSources,
} from '../actions';

function modalForRiver(river, index, dismiss, dispatch) {
  const modal = river.modal || {};
  switch (modal.kind) {
    case 'loading': return <RiverProgress percent={modal.percent} />;
    case 'settings': return <RiverSettings river={river} index={index} />;
    case 'bubble': return <RiverBalloon info={modal.info}  dismiss={dismiss} dispatchAction={dispatch} />;
    default: return <span />;
  }
};

const RiverColumnBase = ({
  rivers,
  index,
  onShowSettings,
  onHideSettings,
  onDismissBalloon,
  dispatch,
  onDropRiver,
}) => {
  const style = {
    backgroundColor: RIVER_COLUMN_BACKGROUND_COLOR,
    borderRadius: 10,
    border: '1px solid ' + COLOR_VERY_DARK,
    height: '100%',
    width: '100%',
  };

  const river = rivers[index] || {};
  const modal = modalForRiver(river, index, onDismissBalloon(index, river), dispatch);

  const onDragOver = (ev) => {
    ev.preventDefault();
    ev.dataTransfer.dropEffect = "move";
  };

  const onDrop = (ev) => {
    ev.preventDefault();
    var source_river = ev.dataTransfer.getData('river');
    if (source_river) {
      onDropRiver(index, river, source_river);
    }
  };

  return (
    <div style={style} onDragOver={onDragOver} onDrop={onDrop}>
      <RiverTitle
        river={river}
        onShowSettings={onShowSettings(index)}
        onHideSettings={onHideSettings(index)}
      />
      {modal}
      <RiverUpdates river={river} index={index} />
    </div>
  );
};

// VisibleRiverColumn
//
const vrc_mapStateToProps = (state) => {
  return {
    rivers: state.rivers,
  };
};
const vrc_mapDispatchToProps = (dispatch) => {
  return {
    onShowSettings: (i, r) => (() => {
      dispatch(showRiverSettings(i));
      if (r.sources === null) {
        dispatch(riverGetFeedSources(i, r));
      }
    }),
    onHideSettings: (i) => (() => dispatch(hideRiverSettings(i))),
    onDismissBalloon: (i) => (() => dispatch(dismissRiverBalloon(i))),
    dispatch: dispatch,
    onDropRiver: (target_index, target_river, dragged_river_id) =>
      dispatch(dropRiver(target_index, target_river, dragged_river_id)),
  };
};

const RiverColumn = connect(
  vrc_mapStateToProps,
  vrc_mapDispatchToProps
)(
  RiverColumnBase
);

export default RiverColumn;
