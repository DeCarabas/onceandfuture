import React from 'react';
import { connect } from 'react-redux';
import {
  RIVER_COLUMN_BACKGROUND_COLOR,
  COLOR_VERY_DARK,

  SIZE_RIVER_MODAL_TOP,
  SIZE_RIVER_TITLE_TOP_SPACER,
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
  const style = {
    position: 'absolute',
    top: SIZE_RIVER_MODAL_TOP,
    width: '100%',
  };
  const modal = river.modal || {};
  let control;
  switch (modal.kind) {
    case 'loading':
      control = <RiverProgress percent={modal.percent} />;
      break;
    case 'settings':
      control = <RiverSettings river={river} index={index} />;
      style.bottom = 0;
      style.overflowY = 'auto';
      break;
    case 'bubble':
      control = <RiverBalloon info={modal.info}  dismiss={dismiss} dispatchAction={dispatch} />;
      break;
    default: return <span />;
  }
  return <div style={style}>
    {control}
  </div>;
}

const RiverTitlePosition = ({river, onShowSettings, onHideSettings}) => {
  const style = {
    position: 'absolute',
    top: SIZE_RIVER_TITLE_TOP_SPACER,
    width: '100%',
  };

  return <div style={style}>
      <RiverTitle
        river={river}
        onShowSettings={onShowSettings}
        onHideSettings={onHideSettings}
      />
  </div>;
};

const RiverBase = ({
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
      <RiverTitlePosition
        river={river}
        onShowSettings={onShowSettings(index, river)}
        onHideSettings={onHideSettings(index)}
      />
      <RiverUpdates river={river} index={index} />
      {modal}
    </div>
  );
};

const mapStateToProps = (state) => {
  return {
    rivers: state.rivers,
  };
};
const mapDispatchToProps = (dispatch) => {
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

const River = connect(mapStateToProps, mapDispatchToProps)(RiverBase);

export default River;
