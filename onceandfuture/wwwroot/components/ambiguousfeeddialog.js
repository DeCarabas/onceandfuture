import React from 'react';
import { connect } from 'react-redux';
import {
  SIZE_COLUMN_WIDTH,
  RIVER_COLUMN_BACKGROUND_COLOR,

  RIVER_SETTINGS_BASE_STYLE,
} from './style';
import Tooltip from './tooltip';
import { SettingsButton } from './settingscontrols';
import {
  hideRiverSettings,
  riverAddFeed,
} from '../actions';

const AmbiguousFeedDialogBase = ({address, feeds, addFeedToRiver, onHideSettings}) => {
  const style = Object.assign({}, RIVER_SETTINGS_BASE_STYLE, {
    position: 'absolute',
  });

  const tableStyle = {
    width: '100%',
    borderSpacing: '0px 4px',
  };

  const subscribeStyleBase = {
    textAlign: 'center',
    cursor: 'pointer',
  };

  const headItemStyle = {
    borderBottom: '1px solid',
  };

  const rows = feeds.map(
    (f, i) => {
      let subscribeStyle = subscribeStyleBase;
      let imgName = '/plus.opt.svg';
      let toolTip = 'Subscribe to this feed.';
      let clickHandler = () => addFeedToRiver(f.feedUrl);

      if (f.isSubscribed) {
        imgName = '/ban.opt.svg';
        toolTip = 'You are already subscribed to this feed.';
        subscribeStyle = Object.assign({}, subscribeStyle, {
          cursor: null,
        });
        clickHandler = () => {};
      }

      return (
        <tr key={i}>
          <td>{f.title}</td>
          <td style={subscribeStyle} onClick={clickHandler}>
            <Tooltip tip={toolTip} position='left'>
              <img src={imgName} width={12} height={12} />
            </Tooltip>
          </td>
        </tr>
      );
    }
  );

  return (
    <div style={style}>
      <div>
        <h3>I found multiple feeds at the address '{address}'.</h3>
        <p>Select the one you want to subscribe to.</p>
      </div>
      <table style={tableStyle}>
        <thead>
          <tr>
            <th style={headItemStyle}>Feed Name</th>
            <th style={headItemStyle}></th>
          </tr>
        </thead>
        <tbody>
          {rows}
        </tbody>
      </table>
      <div>
        <SettingsButton text='Cancel' onClick={onHideSettings} />
      </div>
    </div>
  );
};

const mapStateToProps = () => { return {}; };
const mapDispatchToProps = (dispatch, ownProps) => {
  const index = ownProps.index;
  const river = ownProps.river;

  return {
    addFeedToRiver: (url) => dispatch(riverAddFeed(index, river, url)),
    onHideSettings: () => dispatch(hideRiverSettings(index)),
  };
};

const AmbiguousFeedDialog =
  connect(mapStateToProps, mapDispatchToProps)(AmbiguousFeedDialogBase);


export default AmbiguousFeedDialog;
