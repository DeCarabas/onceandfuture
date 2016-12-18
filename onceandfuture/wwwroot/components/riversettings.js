import React from 'react';
import { connect } from 'react-redux';
import {
  COLOR_DARK,
  COLOR_VERY_LIGHT,
  COLOR_VERY_DARK,

  SIZE_SPACER_HEIGHT,
  SIZE_SPACER_WIDTH,

  Z_INDEX_SETTINGS,
} from './style';
import {
  RIVER_MODE_AUTO,
  RIVER_MODE_TEXT,
  RIVER_MODE_IMAGE,

  removeRiver,
  riverAddFeed,
  riverAddFeedUrlChanged,
  riverRemoveSource,
  riverSetFeedMode,
  riverSetName,
} from '../actions';
import RelTime from './reltime';
import RiverLink from './riverlink';
import { SettingsButton, SettingInputBox} from './settingscontrols';
import Tooltip from './tooltip';

const SettingsSectionTitle = ({text}) => {
  const style = {
    fontWeight: 'bold',
  };

  return <div style={style}>{text}</div>;
}

const AddFeedBox = ({addFeedToRiver}) => {
  return (
    <div>
      <SettingsSectionTitle text="Add A New Site or Feed" />
      <p>Enter the site name or the URL of a feed to subscribe to:</p>
      <SettingInputBox value='' setValue={addFeedToRiver} buttonLabel='Add Feed' />
    </div>
  );
};

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
};

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
      <p>You can choose if you'd rather have this river favor images or text, or automatically decide based
      on the particular entry.</p>
      <div style={{paddingTop: SIZE_SPACER_HEIGHT}}>
        <div style={end_space} />
        <DisplayModeButton
          text={"Auto"}
          enabled={mode === RIVER_MODE_AUTO}
          click={() => setFeedMode(RIVER_MODE_AUTO)}
        />
        <div style={mid_space} />
        <DisplayModeButton
          text={"Image"}
          enabled={mode === RIVER_MODE_IMAGE}
          click={() => setFeedMode(RIVER_MODE_IMAGE)}
        />
        <div style={mid_space} />
        <DisplayModeButton
          text={"Text"}
          enabled={mode === RIVER_MODE_TEXT}
          click={() => setFeedMode(RIVER_MODE_TEXT)}
        />
        <div style={end_space} />
      </div>
    </div>
  );
};

const RenameRiverBox = ({name, setName}) => {
  return (
    <div>
      <SettingsSectionTitle text="Rename This River" />
      <p>Choose a new name for this river.</p>
      <SettingInputBox value={name} setValue={setName} buttonLabel='Rename' />
    </div>
  );
};

const DeleteRiverBox = ({deleteRiver}) => {
  return <div>
    <SettingsSectionTitle text="Remove This River" />
    <p>Do you want to remove this river? Don't worry, you can undo this later if you change your mind.</p>
    <SettingsButton onClick={deleteRiver} text="Remove" />
  </div>;
};

const RiverSource = ({source, deleteSource}) => {
  const timeStyle = {
    textAlign: 'right',
  };
  const unsubscribeStyle = {
    textAlign: 'center',
    cursor: 'pointer',
  };

  const tooltip = <span>Remove this feed.</span>;

  return <tr>
    <td><RiverLink href={source.webUrl}>{source.name}</RiverLink></td>
    <td style={timeStyle}><RelTime time={source.lastUpdated} /></td>
    <td style={unsubscribeStyle} onClick={() => deleteSource(source.id, source.feedUrl)}>
      <Tooltip tip={tooltip} position='left'>
        <i className="fa fa-remove" aria-hidden="true" />
      </Tooltip>
    </td>
  </tr>;
};

const RiverSourcesBox = ({sources, deleteSource}) => {
  const pending_style = {
    textAlign: 'center',
  };
  const error_style = pending_style;

  var tbl_body;
  if (sources === 'PENDING') {
    tbl_body = <tr>
      <td style={pending_style} colSpan='3'>Loading sources, please wait...</td>
    </tr>;
  } else if (sources === 'ERROR') {
    tbl_body = <tr>
      <td style={error_style} colSpan='3'>An unexpected error occurred.</td>
    </tr>;
  } else if (sources) {
    tbl_body = sources.map(
      (s, i) => <RiverSource source={s} key={'s'+i} deleteSource={deleteSource} />
    );
  } else {
    tbl_body = <tr>
      <td style={error_style} colSpan='3'>An unexpected error occurred.</td>
    </tr>;
  }

  const tableStyle = {
    width: '100%',
    borderSpacing: '0px 4px',
  };

  const headItemStyle = {
    borderBottom: '1px solid',
  };

  return <div>
    <SettingsSectionTitle text="Feeds" />
    <p>This river is subscribed to these feeds:</p>
    <table style={tableStyle}>
      <thead>
        <tr>
          <th style={headItemStyle}>Feed Name</th>
          <th style={headItemStyle}>Last Updated</th>
          <th style={headItemStyle}></th>
        </tr>
      </thead>
      <tbody>
        { tbl_body }
      </tbody>
    </table>
  </div>;
};

const RiverSettingsBase = ({
  index,
  river,
  user,
  feedUrlChanged,
  addFeedToRiver,
  riverSetFeedMode,
  deleteRiver,
  removeSource,
  setRiverName,
}) => {
  const style = {
    position: 'absolute',

    backgroundColor: COLOR_VERY_LIGHT,
    zIndex: Z_INDEX_SETTINGS,
    padding: SIZE_SPACER_WIDTH,
    border: '1px solid ' + COLOR_VERY_DARK,
  };

  const addFeed = (url) => addFeedToRiver(index, river, url);
  const urlChanged = (text) => feedUrlChanged(index, text);
  const setFeedMode = (mode) => riverSetFeedMode(index, river, mode);
  const delRiver = () => deleteRiver(user, river);
  const delSource = (source_id, source_url) => removeSource(index, river, source_id, source_url);
  const setName = (name) => setRiverName(index, river, name);

  return <div style={style}>
    <AddFeedBox feedUrlChanged={urlChanged} addFeedToRiver={addFeed} />
    <hr />
    <FeedDisplayModeBox mode={river.mode} setFeedMode={setFeedMode} />
    <hr />
    <RiverSourcesBox sources={river.sources} deleteSource={delSource} />
    <hr />
    <RenameRiverBox name={river.name} setName={setName}/>
    <hr />
    <DeleteRiverBox deleteRiver={delRiver} />
  </div>;
};

const mapStateToProps = (state) => {
  return {
    user: state.user,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    feedUrlChanged: (index, new_value) => dispatch(riverAddFeedUrlChanged(index, new_value)),
    addFeedToRiver: (index, river, url) => dispatch(riverAddFeed(index, river, url)),
    riverSetFeedMode: (index, river, mode) => dispatch(riverSetFeedMode(index, river, mode)),
    deleteRiver: (user, river) => dispatch(removeRiver(user, river)),
    removeSource: (index, river, source_id, source_url) =>
      dispatch(riverRemoveSource(index, river, source_id, source_url)),
    setRiverName: (index, river, new_name) => dispatch(riverSetName(index, river, new_name)),
  };
};

const RiverSettings =
  connect(mapStateToProps, mapDispatchToProps)(RiverSettingsBase);

export default RiverSettings;
