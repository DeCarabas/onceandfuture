var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux'
import {
  COLUMNSPACER,
  COLUMNWIDTH,
  COLOR_DARK,
  COLOR_VERY_LIGHT,
  COLOR_VERY_DARK,
} from './style'
import {
  RIVER_MODE_AUTO,
  RIVER_MODE_TEXT,
  RIVER_MODE_IMAGE,

  addFeedToRiver,
  riverAddFeedUrlChanged,
  riverSetFeedMode,
} from '../actions'

const SettingsSectionTitle = ({text}) => {
  const style = {
    fontWeight: 'bold',
  };

  return <div style={style}>{text}</div>;
}

const SettingsButton = ({onClick, text}) => {
  const divStyle = {
    textAlign: 'right',
    marginTop: COLUMNSPACER,
  }
  const style = {
    color: 'white',
    backgroundColor: COLOR_DARK,
    padding: 3,
    border: '2px solid ' + COLOR_VERY_DARK,
    cursor: 'pointer',
  }

  return (
    <div style={divStyle}>
      <span style={style} onClick={onClick}>{text}</span>
    </div>
  );
}

const AddFeedBoxUrl = ({onChange}) => {
  const style = {
    width: '100%',
  }
  return <div style={style}>
    <input
      style={style}
      type="text"
      onChange={ (e) => onChange(e.target.value) }
    />
  </div>;
}

const AddFeedBox = ({feedUrlChanged, addFeedToRiver}) => {
  return (
    <div>
      <SettingsSectionTitle text="Add A New Site or Feed" />
      <AddFeedBoxUrl onChange={feedUrlChanged} />
      <SettingsButton onClick={addFeedToRiver} text="Add Feed" />
    </div>
  );
}

const DisplayModeButton = ({text, enabled, click}) => {
  const butt = {
    width: 100,
    display: 'inline-block',
    textAlign: 'center',
    border: '2px solid ' + COLOR_VERY_DARK,
    cursor: 'pointer',
    backgroundColor: enabled ? COLOR_DARK : null,
  };

  return <div style={butt} onClick={click}>{text}</div>;
}

const FeedDisplayModeBox = ({mode, setFeedMode}) => {
  const end_space = {
    width: 10,
    display: 'inline-block',
  };
  const mid_space = {
    width: 4,
    display: 'inline-block',
  };

  return (
    <div>
      <SettingsSectionTitle text="River Display Mode" />
      <div style={{paddingTop: COLUMNSPACER}}>
        <div style={end_space} />
        <DisplayModeButton text={"Auto"} enabled={mode === RIVER_MODE_AUTO} click={() => setFeedMode(RIVER_MODE_AUTO)} />
        <div style={mid_space} />
        <DisplayModeButton text={"Image"} enabled={mode === RIVER_MODE_IMAGE} click={() => setFeedMode(RIVER_MODE_IMAGE)} />
        <div style={mid_space} />
        <DisplayModeButton text={"Text"} enabled={mode === RIVER_MODE_TEXT} click={() => setFeedMode(RIVER_MODE_TEXT)} />
        <div style={end_space} />
      </div>
    </div>
  );
}

const RiverSettingsBase = ({
  index,
  river,
  feedUrlChanged,
  addFeedToRiver,
  riverSetFeedMode
}) => {
  const style = {
    backgroundColor: COLOR_VERY_LIGHT,
    zIndex: 3,
    position: 'absolute',
    left: 0,
    right: 0,
    padding: COLUMNSPACER,
    border: '1px solid ' + COLOR_VERY_DARK,
  };

  const addFeed = () => addFeedToRiver(index, river);
  const urlChanged = (text) => feedUrlChanged(index, text);
  const setFeedMode = (mode) => riverSetFeedMode(index, river, mode);

  return <div style={style}>
    <AddFeedBox feedUrlChanged={urlChanged} addFeedToRiver={addFeed} />
    <FeedDisplayModeBox mode={river.mode} setFeedMode={setFeedMode} />
  </div>;
}

const mapStateToProps = (state) => {
  return {
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    'feedUrlChanged': (index, new_value) =>
      dispatch(riverAddFeedUrlChanged(index, new_value)),
    'addFeedToRiver': (index, river) =>
      dispatch(addFeedToRiver(index, river)),
    'riverSetFeedMode': (index, river, mode) =>
      dispatch(riverSetFeedMode(index, river, mode)),
  };
};

const RiverSettings =
  connect(mapStateToProps, mapDispatchToProps)(RiverSettingsBase);

export default RiverSettings;
